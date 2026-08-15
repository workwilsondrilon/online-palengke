using System.Globalization;
using System.Text;
using System.Text.Json;

namespace OnlinePalengke.Domain.Markets;

/// <summary>
/// Converts a market's service-area polygon between the two representations that meet at
/// its edges: GeoJSON -- what Leaflet.draw produces and consumes in the Admin UI, always
/// longitude-latitude per the GeoJSON spec -- and WKT under SRID 4326, what
/// <see cref="Market.ServiceAreaWkt"/> holds and what MySQL 8 expects and returns from
/// <c>ST_GeomFromText</c> / <c>ST_AsText</c>: always latitude-longitude, the EPSG:4326
/// standard axis order. Verified directly against the local server -- see the header
/// comment of migration script 002.
/// </summary>
/// <remarks>
/// This axis flip is the single easiest thing to get wrong in Epic 3: swapping it does not
/// throw for most real-world coordinates (both axes are ordinary-looking numbers), it just
/// silently geofences the wrong place. The flip happens in exactly two places in this
/// method pair, both commented, and both covered by round-trip tests against real
/// coordinates rather than arbitrary ones.
/// <para>
/// Only single-ring polygons are supported. A market's service area has no interior holes
/// and is never a <c>MultiPolygon</c> -- both are rejected outright rather than silently
/// truncated to the first ring, which would produce a plausible-looking but wrong area.
/// </para>
/// </remarks>
public static class PolygonWkt
{
    /// <summary>Tolerance for treating a ring's first and last position as "the same point".</summary>
    private const double ClosureTolerance = 1e-9;

    /// <summary>
    /// Parses a GeoJSON Polygon (positions as [longitude, latitude]) into WKT under SRID
    /// 4326 (coordinate pairs as "latitude longitude").
    /// </summary>
    /// <exception cref="FormatException">
    /// The input is not a single-ring, closed GeoJSON Polygon with at least 3 distinct
    /// vertices.
    /// </exception>
    public static string FromGeoJson(string geoJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(geoJson);

        using var document = JsonDocument.Parse(geoJson);
        var root = document.RootElement;

        if (!root.TryGetProperty("type", out var typeProperty) ||
            !string.Equals(typeProperty.GetString(), "Polygon", StringComparison.Ordinal))
        {
            throw new FormatException(
                "Only a GeoJSON Polygon is supported for a market service area (not MultiPolygon or another geometry type).");
        }

        if (!root.TryGetProperty("coordinates", out var coordinates) || coordinates.ValueKind != JsonValueKind.Array)
        {
            throw new FormatException("The GeoJSON Polygon has no coordinates.");
        }

        var rings = coordinates.EnumerateArray().ToList();
        if (rings.Count == 0)
        {
            throw new FormatException("The GeoJSON Polygon has no rings.");
        }

        if (rings.Count > 1)
        {
            throw new FormatException("Polygons with holes (interior rings) are not supported for a market service area.");
        }

        var points = rings[0].EnumerateArray().Select(ParseGeoJsonPosition).ToList();
        RequireClosedRing(points, "GeoJSON");

        var builder = new StringBuilder("POLYGON((");
        for (var i = 0; i < points.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(", ");
            }

            // The flip: GeoJSON is (lng, lat); WKT under SRID 4326 is (lat, lng).
            builder.Append(points[i].Lat.ToString(CultureInfo.InvariantCulture));
            builder.Append(' ');
            builder.Append(points[i].Lng.ToString(CultureInfo.InvariantCulture));
        }

        builder.Append("))");
        return builder.ToString();
    }

    /// <summary>
    /// Formats WKT under SRID 4326 (coordinate pairs as "latitude longitude") back into a
    /// GeoJSON Polygon (positions as [longitude, latitude]) -- the shape Leaflet.draw
    /// expects.
    /// </summary>
    /// <exception cref="FormatException">The input is not a single-ring WKT POLYGON.</exception>
    public static string ToGeoJson(string wkt)
    {
        var points = ParseWktPolygon(wkt);
        RequireClosedRing(points, "WKT");

        var builder = new StringBuilder("""{"type":"Polygon","coordinates":[[""");
        for (var i = 0; i < points.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(',');
            }

            // The flip: WKT under SRID 4326 is (lat, lng); GeoJSON is (lng, lat).
            builder.Append('[');
            builder.Append(points[i].Lng.ToString(CultureInfo.InvariantCulture));
            builder.Append(',');
            builder.Append(points[i].Lat.ToString(CultureInfo.InvariantCulture));
            builder.Append(']');
        }

        builder.Append("]]}");
        return builder.ToString();
    }

    private static (double Lat, double Lng) ParseGeoJsonPosition(JsonElement position)
    {
        if (position.ValueKind != JsonValueKind.Array)
        {
            throw new FormatException("Each GeoJSON position must be a [longitude, latitude] array.");
        }

        var values = position.EnumerateArray().Select(e => e.GetDouble()).ToList();
        if (values.Count < 2)
        {
            throw new FormatException("Each GeoJSON position needs at least a longitude and a latitude.");
        }

        // GeoJSON order is [longitude, latitude].
        return (Lat: values[1], Lng: values[0]);
    }

    private static List<(double Lat, double Lng)> ParseWktPolygon(string wkt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(wkt);

        var trimmed = wkt.Trim();
        const string prefix = "POLYGON((";
        const string suffix = "))";

        if (!trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
            !trimmed.EndsWith(suffix, StringComparison.Ordinal))
        {
            throw new FormatException($"Expected a single-ring WKT POLYGON, got: '{wkt}'.");
        }

        var inner = trimmed[prefix.Length..^suffix.Length];
        var points = new List<(double Lat, double Lng)>();

        foreach (var pair in inner.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2)
            {
                throw new FormatException($"Malformed WKT coordinate pair: '{pair}'.");
            }

            var lat = double.Parse(parts[0], CultureInfo.InvariantCulture);
            var lng = double.Parse(parts[1], CultureInfo.InvariantCulture);
            points.Add((lat, lng));
        }

        return points;
    }

    private static void RequireClosedRing(List<(double Lat, double Lng)> points, string format)
    {
        if (points.Count < 4)
        {
            throw new FormatException(
                $"A polygon ring needs at least 4 positions (3 distinct vertices plus the closing point), the {format} input had {points.Count}.");
        }

        var first = points[0];
        var last = points[^1];
        if (Math.Abs(first.Lat - last.Lat) > ClosureTolerance || Math.Abs(first.Lng - last.Lng) > ClosureTolerance)
        {
            throw new FormatException($"The {format} polygon ring is not closed -- its first and last positions must match.");
        }
    }
}
