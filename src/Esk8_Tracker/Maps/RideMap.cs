using BruTile.Cache;
using Mapsui;
using Mapsui.Layers;
using Mapsui.Nts;
using Mapsui.Projections;
using Mapsui.Styles;
using Mapsui.Tiling;
using NetTopologySuite.Geometries;
using Map = Mapsui.Map;
using Color = Mapsui.Styles.Color;

namespace Esk8_Tracker.Maps;

/// <summary>
/// Wraps all Mapsui specifics: OSM base layer (with persistent tile cache),
/// a red route polyline, and the my-location marker. Callers speak lat/lon;
/// conversion to spherical-mercator map coordinates happens here.
/// </summary>
public class RideMap
{
    private readonly MemoryLayer _routeLayer;
    private readonly MyLocationLayer _locationLayer;
    private readonly List<Coordinate> _routeCoords = new();

    public Map Map { get; }

    public RideMap()
    {
        Map = new Map();

        // OSM tile-usage policy wants an identifying user agent; FileCache keeps
        // tiles on disk so frequently ridden areas work with patchy connectivity.
        OpenStreetMap.DefaultCache ??= new FileCache(
            Path.Combine(FileSystem.CacheDirectory, "osm-tiles"), "png");
        Map.Layers.Add(OpenStreetMap.CreateTileLayer("Esk8_Tracker/1.0 (personal ride tracker)"));

        _routeLayer = new MemoryLayer
        {
            Name = "Route",
            Features = Array.Empty<IFeature>(),
            Style = new VectorStyle { Line = new Pen { Color = Color.Red, Width = 4 } },
        };
        Map.Layers.Add(_routeLayer);

        _locationLayer = new MyLocationLayer(Map) { IsCentered = true };
        Map.Layers.Add(_locationLayer);
    }

    public void UpdatePosition(double lat, double lon)
    {
        var (x, y) = SphericalMercator.FromLonLat(lon, lat);
        _locationLayer.UpdateMyLocation(new MPoint(x, y), animated: true);
    }

    public void AppendRoutePoint(double lat, double lon)
    {
        var (x, y) = SphericalMercator.FromLonLat(lon, lat);
        _routeCoords.Add(new Coordinate(x, y));
        RebuildRouteLayer();
    }

    public void SetRoute(IEnumerable<(double Lat, double Lon)> points)
    {
        _routeCoords.Clear();
        foreach (var (lat, lon) in points)
        {
            var (x, y) = SphericalMercator.FromLonLat(lon, lat);
            _routeCoords.Add(new Coordinate(x, y));
        }
        RebuildRouteLayer();
    }

    public void ClearRoute()
    {
        _routeCoords.Clear();
        RebuildRouteLayer();
    }

    public void CenterOn(double lat, double lon)
    {
        var (x, y) = SphericalMercator.FromLonLat(lon, lat);
        // Resolutions[] runs from world (0) to street level; 17 is a riding zoom.
        var resolutions = Map.Navigator.Resolutions;
        var resolution = resolutions.Count > 17 ? resolutions[17] : resolutions[^1];
        Map.Navigator.CenterOnAndZoomTo(new MPoint(x, y), resolution);
    }

    public void ZoomToRoute()
    {
        if (_routeCoords.Count < 2) return;
        var rect = new MRect(
            _routeCoords.Min(c => c.X), _routeCoords.Min(c => c.Y),
            _routeCoords.Max(c => c.X), _routeCoords.Max(c => c.Y));
        Map.Navigator.ZoomToBox(rect.Grow(Math.Max(rect.Width, rect.Height) * 0.1 + 50));
    }

    private void RebuildRouteLayer()
    {
        // NTS LineString needs >= 2 coordinates.
        _routeLayer.Features = _routeCoords.Count < 2
            ? Array.Empty<IFeature>()
            : new[] { new GeometryFeature { Geometry = new LineString(_routeCoords.ToArray()) } };
        _routeLayer.DataHasChanged();
    }
}
