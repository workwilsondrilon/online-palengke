using System.Globalization;
using OnlinePalengke.Domain.Markets;

namespace OnlinePalengke.UnitTests.Domain;

/// <summary>
/// <see cref="PolygonWkt"/> exists because MySQL 8's SRID 4326 axis order (latitude,
/// longitude) is the reverse of GeoJSON's (longitude, latitude) -- verified against the
/// real local server while writing migration 002. Getting the flip backwards would not
/// throw for realistic coordinates, it would silently geofence the wrong place, so every
/// test here uses the same real Balintawak-area coordinates that were checked against
/// live MySQL, not arbitrary numbers.
/// </summary>
public sealed class PolygonWktTests
{
    // A box over Balintawak Market: lng 120.985-121.015, lat 14.655-14.680. The same
    // coordinates used in the migration 002 smoke test and in MarketDetail.razor's sample.
    private const string BalintawakGeoJson =
        """{"type":"Polygon","coordinates":[[[120.985,14.655],[121.015,14.655],[121.015,14.68],[120.985,14.68],[120.985,14.655]]]}""";

    private const string BalintawakWkt =
        "POLYGON((14.655 120.985, 14.655 121.015, 14.68 121.015, 14.68 120.985, 14.655 120.985))";

    [Fact]
    public void FromGeoJson_flips_lng_lat_to_lat_lng()
    {
        var wkt = PolygonWkt.FromGeoJson(BalintawakGeoJson);

        Assert.Equal(BalintawakWkt, wkt);
    }

    [Fact]
    public void ToGeoJson_flips_lat_lng_back_to_lng_lat()
    {
        var geoJson = PolygonWkt.ToGeoJson(BalintawakWkt);

        Assert.Equal(BalintawakGeoJson, geoJson);
    }

    [Fact]
    public void Round_trips_through_both_conversions()
    {
        var wkt = PolygonWkt.FromGeoJson(BalintawakGeoJson);
        var backToGeoJson = PolygonWkt.ToGeoJson(wkt);

        Assert.Equal(BalintawakGeoJson, backToGeoJson);
    }

    [Fact]
    public void FromGeoJson_produces_coordinates_within_valid_lat_lng_ranges()
    {
        // The regression this guards: if the flip were wrong, "120.985" would land in the
        // latitude slot, which MySQL's ST_GeomFromText rejects outright ("Latitude
        // 120.985000 is out of range") -- confirmed against the real server. Every
        // latitude produced here must be in [-90, 90].
        var wkt = PolygonWkt.FromGeoJson(BalintawakGeoJson);

        var inner = wkt[9..^2]; // strip the "POLYGON((" prefix and "))" suffix
        foreach (var pair in inner.Split(", "))
        {
            var lat = double.Parse(pair.Split(' ')[0], CultureInfo.InvariantCulture);
            Assert.InRange(lat, -90, 90);
        }
    }

    [Fact]
    public void FromGeoJson_rejects_a_multi_polygon()
    {
        const string multiPolygon = """{"type":"MultiPolygon","coordinates":[[[[120.985,14.655],[121.015,14.655],[121.015,14.68],[120.985,14.655]]]]}""";

        Assert.Throws<FormatException>(() => PolygonWkt.FromGeoJson(multiPolygon));
    }

    [Fact]
    public void FromGeoJson_rejects_a_polygon_with_a_hole()
    {
        const string withHole =
            """{"type":"Polygon","coordinates":[[[120.9,14.6],[121.1,14.6],[121.1,14.8],[120.9,14.6]],[[121.0,14.7],[121.05,14.7],[121.05,14.75],[121.0,14.7]]]}""";

        Assert.Throws<FormatException>(() => PolygonWkt.FromGeoJson(withHole));
    }

    [Fact]
    public void FromGeoJson_rejects_an_unclosed_ring()
    {
        const string unclosed = """{"type":"Polygon","coordinates":[[[120.985,14.655],[121.015,14.655],[121.015,14.68]]]}""";

        Assert.Throws<FormatException>(() => PolygonWkt.FromGeoJson(unclosed));
    }

    [Fact]
    public void FromGeoJson_rejects_too_few_vertices()
    {
        const string tooFew = """{"type":"Polygon","coordinates":[[[120.985,14.655],[121.015,14.655],[120.985,14.655]]]}""";

        Assert.Throws<FormatException>(() => PolygonWkt.FromGeoJson(tooFew));
    }

    [Fact]
    public void ToGeoJson_rejects_malformed_wkt()
    {
        Assert.Throws<FormatException>(() => PolygonWkt.ToGeoJson("MULTIPOLYGON(((0 0, 1 0, 1 1, 0 0)))"));
    }
}
