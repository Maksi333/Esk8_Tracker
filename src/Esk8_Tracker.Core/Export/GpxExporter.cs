using System.Globalization;
using System.Text;
using System.Xml;
using Esk8_Tracker.Core.Models;

namespace Esk8_Tracker.Core.Export;

/// <summary>Writes a ride as standards-compliant GPX 1.1 (readable by every GPS tool).</summary>
public static class GpxExporter
{
    public static string ToGpx(Ride ride, IReadOnlyList<TrackPoint> points)
    {
        var sb = new StringBuilder();
        var settings = new XmlWriterSettings { Indent = true, Encoding = new UTF8Encoding(false) };
        using (var w = XmlWriter.Create(sb, settings))
        {
            w.WriteStartDocument();
            w.WriteStartElement("gpx", "http://www.topografix.com/GPX/1/1");
            w.WriteAttributeString("version", "1.1");
            w.WriteAttributeString("creator", "ESK8 Tracker");

            w.WriteStartElement("metadata");
            w.WriteElementString("name", DisplayName(ride));
            w.WriteElementString("time", Iso(ride.StartedAt));
            w.WriteEndElement();

            w.WriteStartElement("trk");
            w.WriteElementString("name", DisplayName(ride));
            if (ride.Notes.Length > 0)
                w.WriteElementString("desc", ride.Notes);
            w.WriteStartElement("trkseg");
            foreach (var p in points)
            {
                w.WriteStartElement("trkpt");
                w.WriteAttributeString("lat", p.Latitude.ToString("F7", CultureInfo.InvariantCulture));
                w.WriteAttributeString("lon", p.Longitude.ToString("F7", CultureInfo.InvariantCulture));
                if (p.AltitudeMeters is { } ele)
                    w.WriteElementString("ele", ele.ToString("F1", CultureInfo.InvariantCulture));
                w.WriteElementString("time", Iso(p.Timestamp));
                w.WriteEndElement();
            }
            w.WriteEndElement(); // trkseg
            w.WriteEndElement(); // trk
            w.WriteEndElement(); // gpx
        }
        return sb.ToString();
    }

    public static string FileName(Ride ride) =>
        $"esk8-{ride.StartedAt.ToLocalTime():yyyy-MM-dd-HHmm}.gpx";

    private static string DisplayName(Ride ride) =>
        ride.Name.Length > 0 ? ride.Name : $"Ride {ride.StartedAt.ToLocalTime():yyyy-MM-dd HH:mm}";

    private static string Iso(DateTime utc) =>
        DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
}
