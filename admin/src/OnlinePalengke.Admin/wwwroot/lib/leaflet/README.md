# Leaflet + Leaflet.draw — vendoring note

The market detail page (`/markets/{id}`) will host a **Leaflet + Leaflet.draw** polygon
editor for a market's delivery service area. The shell deliberately ships **without** the
library so that nothing is fetched from a CDN and the app keeps working offline. This
folder is the seam.

## What to drop in here

Copy these four files into `admin/wwwroot/lib/leaflet/` (flat, no sub-folders), plus
Leaflet's `images/` folder, which its CSS references by relative path:

| File | From |
| --- | --- |
| `leaflet.css` | leaflet `dist/leaflet.css` |
| `leaflet.js` | leaflet `dist/leaflet.js` |
| `images/*` | leaflet `dist/images/` (marker icons, shadow) |
| `leaflet.draw.css` | leaflet-draw `dist/leaflet.draw.css` |
| `leaflet.draw.js` | leaflet-draw `dist/leaflet.draw.js` |
| `images/*` | leaflet-draw `dist/images/` (the draw toolbar sprite) |

Pinned versions for this project: **Leaflet 1.9.x** and **Leaflet.draw 1.0.4**. Both are
BSD-2-Clause / MIT. Obtain them through whichever supply-chain route this team already
uses (npm/`libman`/an internal mirror) rather than an ad-hoc download — then commit them,
because the build has no npm step.

If you prefer LibMan, `libman install leaflet@1.9.4 --provider jsdelivr --destination
wwwroot/lib/leaflet` reproduces the same layout.

## Wiring it up

1. **Uncomment the four tags in `Components/App.razor`.** They are already written out,
   marked `LEAFLET SEAM`, and use `@Assets[...]` so they get fingerprinted like every other
   static asset. Two `<link>` tags in `<head>`, two `<script>` tags after
   `blazor.web.js` in `<body>`.

2. **Implement `Components/Markets/ServiceAreaMap.razor`.** It currently renders only a
   sized placeholder with the same dimensions the live map will occupy. The component
   already declares the parameters the real editor needs (`MarketId`,
   `InitialPolygonGeoJson`, `OnPolygonChanged`) and holds the `_mapHost` element
   reference, so the change is confined to that one file:

   - add a `wwwroot/js/service-area-map.js` ES module exporting `init`, `load`, `destroy`;
   - `@inject IJSRuntime`, import the module in `OnAfterRenderAsync(firstRender: true)`;
   - hand the drawn GeoJSON back through `OnPolygonChanged` via `[JSInvokable]`;
   - implement `IAsyncDisposable` to tear the map down when the circuit ends.

3. **Tiles.** Leaflet needs a tile source. `openstreetmap.org` tiles are a network call, so
   decide the provider (self-hosted, MapTiler key, or an offline `.mbtiles` proxy) before
   this reaches an air-gapped environment.

Nothing outside `ServiceAreaMap.razor`, `App.razor` and this folder should need to change.
