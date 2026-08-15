// Leaflet + Leaflet.draw glue for Components/Markets/ServiceAreaMap.razor.
//
// One Leaflet map instance per element id, tracked in `maps` so repeated interop calls
// (load/destroy) from Blazor - which carry no JS-side object reference of their own - can
// find the right instance. A service area is always at most one polygon: drawing a new
// one, or finishing an edit, replaces whatever was in `drawnItems` rather than adding to it.

const maps = new Map();

// Metro Manila. Only used when a map has no saved polygon yet to fit bounds to - every
// market this app manages is somewhere in the Philippines, so this keeps a brand-new
// market's map from opening zoomed out to whole-world.
const DEFAULT_CENTER = [14.5995, 120.9842];
const DEFAULT_ZOOM = 12;

export function init(elementId, dotNetRef, options) {
    const host = document.getElementById(elementId);
    if (!host || maps.has(elementId)) {
        return;
    }

    const map = L.map(host, {
        center: DEFAULT_CENTER,
        zoom: DEFAULT_ZOOM,
    });

    L.tileLayer("https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png", {
        maxZoom: 19,
        attribution: "&copy; <a href=\"https://www.openstreetmap.org/copyright\">OpenStreetMap</a> contributors",
    }).addTo(map);

    const drawnItems = new L.FeatureGroup();
    map.addLayer(drawnItems);

    const readOnly = options?.readOnly === true;

    if (!readOnly) {
        const drawControl = new L.Control.Draw({
            edit: {
                featureGroup: drawnItems,
                remove: true,
            },
            draw: {
                polygon: {
                    allowIntersection: false,
                    showArea: true,
                },
                // A service area is one polygon - every other shape tool is noise.
                polyline: false,
                rectangle: false,
                circle: false,
                circlemarker: false,
                marker: false,
            },
        });
        map.addControl(drawControl);

        map.on(L.Draw.Event.CREATED, (e) => {
            // Only one polygon at a time - a freshly drawn one replaces the old.
            drawnItems.clearLayers();
            drawnItems.addLayer(e.layer);
            emitChange(dotNetRef, drawnItems);
        });

        map.on(L.Draw.Event.EDITED, () => emitChange(dotNetRef, drawnItems));
        map.on(L.Draw.Event.DELETED, () => emitChange(dotNetRef, drawnItems));
    }

    maps.set(elementId, { map, drawnItems });

    applyGeoJson(elementId, options?.initialGeoJson ?? null, { fit: true });
}

export function load(elementId, geoJson) {
    applyGeoJson(elementId, geoJson, { fit: true });
}

export function destroy(elementId) {
    const entry = maps.get(elementId);
    if (!entry) {
        return;
    }

    entry.map.off();
    entry.map.remove();
    maps.delete(elementId);
}

function applyGeoJson(elementId, geoJsonString, { fit }) {
    const entry = maps.get(elementId);
    if (!entry) {
        return;
    }

    const { map, drawnItems } = entry;
    drawnItems.clearLayers();

    if (!geoJsonString) {
        return;
    }

    const geometry = JSON.parse(geoJsonString);
    const layer = L.geoJSON(geometry);
    layer.eachLayer((l) => drawnItems.addLayer(l));

    if (fit && drawnItems.getLayers().length > 0) {
        map.fitBounds(drawnItems.getBounds(), { padding: [24, 24] });
    }
}

function emitChange(dotNetRef, drawnItems) {
    const layers = drawnItems.getLayers();
    const geoJson = layers.length > 0 ? JSON.stringify(layers[0].toGeoJSON().geometry) : null;
    dotNetRef.invokeMethodAsync("OnPolygonEditedFromJs", geoJson);
}
