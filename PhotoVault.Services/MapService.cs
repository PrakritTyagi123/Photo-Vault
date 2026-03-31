using PhotoVault.Core.Data;

namespace PhotoVault.Services;

public class MapService
{
    private readonly DatabaseService _db;
    public MapService(DatabaseService db) { _db = db; }

    public List<MapPoint> GetMapPoints()
    {
        var pts = new List<MapPoint>(); using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = "SELECT id,file_name,latitude,longitude,city,country,thumbnail_small,media_type,date_taken,date_imported FROM media WHERE latitude IS NOT NULL AND longitude IS NOT NULL ORDER BY COALESCE(date_taken,date_imported) DESC";
        using var r = cmd.ExecuteReader();
        while (r.Read()) pts.Add(new MapPoint { MediaId = r.GetInt64(0), FileName = r.GetString(1), Latitude = r.GetDouble(2), Longitude = r.GetDouble(3), City = r.IsDBNull(4) ? "" : r.GetString(4), Country = r.IsDBNull(5) ? "" : r.GetString(5), ThumbnailPath = r.IsDBNull(6) ? null : r.GetString(6), MediaType = r.IsDBNull(7) ? "Photo" : r.GetString(7) });
        return pts;
    }

    public List<MapCluster> GetClusters(double grid = 0.5)
    {
        var pts = GetMapPoints(); var clusters = new List<MapCluster>(); var used = new HashSet<int>();
        for (int i = 0; i < pts.Count; i++)
        {
            if (used.Contains(i)) continue;
            var c = new MapCluster { Latitude = pts[i].Latitude, Longitude = pts[i].Longitude, Count = 1, City = pts[i].City, Country = pts[i].Country };
            for (int j = i + 1; j < pts.Count; j++) { if (used.Contains(j)) continue; if (Math.Abs(pts[i].Latitude - pts[j].Latitude) < grid && Math.Abs(pts[i].Longitude - pts[j].Longitude) < grid) { c.Count++; used.Add(j); } }
            clusters.Add(c); used.Add(i);
        }
        return clusters.OrderByDescending(c => c.Count).ToList();
    }

    public int GetGpsCount() { using var cmd = _db.Connection.CreateCommand(); cmd.CommandText = "SELECT COUNT(*) FROM media WHERE latitude IS NOT NULL"; return Convert.ToInt32(cmd.ExecuteScalar()); }

    /// <summary>Generate HTML for Leaflet.js map with all GPS points</summary>
    public string GenerateMapHtml()
    {
        var points = GetMapPoints();
        var markers = string.Join(",\n", points.Select(p =>
            $"[{p.Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)},{p.Longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)},\"{p.FileName.Replace("\"", "\\\"")}\",\"{p.City}, {p.Country}\"]"));

        return $@"<!DOCTYPE html>
<html><head>
<meta charset='utf-8'/>
<link rel='stylesheet' href='https://unpkg.com/leaflet@1.9.4/dist/leaflet.css'/>
<script src='https://unpkg.com/leaflet@1.9.4/dist/leaflet.js'></script>
<style>body{{margin:0;padding:0}}#map{{width:100%;height:100vh}}</style>
</head><body>
<div id='map'></div>
<script>
var map = L.map('map').setView([20,0],2);
L.tileLayer('https://{{s}}.tile.openstreetmap.org/{{z}}/{{x}}/{{y}}.png',{{attribution:'OSM',maxZoom:19}}).addTo(map);
var pts = [{markers}];
var markers = L.markerClusterGroup ? L.markerClusterGroup() : L.layerGroup();
pts.forEach(function(p){{
    var m = L.circleMarker([p[0],p[1]],{{radius:6,fillColor:'#7c9bf5',color:'#5a7de0',weight:1,fillOpacity:0.8}});
    m.bindPopup('<b>'+p[2]+'</b><br/>'+p[3]);
    markers.addLayer(m);
}});
markers.addTo(map);
if(pts.length>0){{var b=L.latLngBounds(pts.map(function(p){{return[p[0],p[1]]}}));map.fitBounds(b,{{padding:[30,30]}});}}
</script>
</body></html>";
    }
}

public class MapPoint
{
    public long MediaId { get; set; } public string FileName { get; set; } = ""; public double Latitude { get; set; } public double Longitude { get; set; }
    public string City { get; set; } = ""; public string Country { get; set; } = ""; public string? ThumbnailPath { get; set; } public string MediaType { get; set; } = "Photo";
}

public class MapCluster
{
    public double Latitude { get; set; } public double Longitude { get; set; } public int Count { get; set; }
    public string City { get; set; } = ""; public string Country { get; set; } = "";
}
