using System.Net.Http;
using System.Text.Json;
using PhotoVault.Core.Data;
using PhotoVault.Core.Models;

namespace PhotoVault.Services;

public class GeocodingService
{
    private readonly DatabaseService _db;
    private readonly LogService _log;
    private static readonly HttpClient _http = new();
    private DateTime _lastCall = DateTime.MinValue;

    public GeocodingService(DatabaseService db, LogService log) { _db = db; _log = log; _http.DefaultRequestHeaders.UserAgent.ParseAdd("PhotoVault/1.0"); }

    public async Task<int> GeocodeAllAsync(IProgress<(int done, int total)>? progress = null, CancellationToken ct = default)
    {
        var items = GetNeedingGeocode();
        int done = 0, ok = 0;
        _log.Info("Geocoding", $"{items.Count} items need geocoding");
        foreach (var item in items)
        {
            if (ct.IsCancellationRequested) break;
            if (item.Latitude.HasValue && item.Longitude.HasValue)
            {
                var r = await ReverseGeocodeAsync(item.Latitude.Value, item.Longitude.Value);
                if (r != null) { UpdateLocation(item.Id, r.City, r.Country, r.Address); ok++; }
            }
            done++; progress?.Report((done, items.Count));
        }
        _log.Info("Geocoding", $"Geocoded {ok}/{items.Count}");
        return ok;
    }

    private async Task<GeoResult?> ReverseGeocodeAsync(double lat, double lon)
    {
        try
        {
            var elapsed = DateTime.Now - _lastCall;
            if (elapsed.TotalMilliseconds < 1100) await Task.Delay(1100 - (int)elapsed.TotalMilliseconds);

            var url = $"https://nominatim.openstreetmap.org/reverse?format=json&lat={lat.ToString(System.Globalization.CultureInfo.InvariantCulture)}&lon={lon.ToString(System.Globalization.CultureInfo.InvariantCulture)}&zoom=14&addressdetails=1";
            _lastCall = DateTime.Now;
            var json = JsonDocument.Parse(await _http.GetStringAsync(url));

            if (json.RootElement.TryGetProperty("address", out var addr))
            {
                var city = First(addr, "city", "town", "village", "municipality", "county");
                var country = Prop(addr, "country");
                var state = First(addr, "state", "region", "province");
                var road = Prop(addr, "road");
                var display = string.Join(", ", new[] { road, city, state }.Where(s => !string.IsNullOrEmpty(s)));
                return new GeoResult { City = city ?? "", Country = country ?? "", State = state ?? "", Address = display };
            }
        }
        catch (Exception ex) { _log.Debug("Geocoding", $"({lat:F4},{lon:F4}): {ex.Message}"); }
        return null;
    }

    private static string? Prop(JsonElement el, string name) => el.TryGetProperty(name, out var p) ? p.GetString() : null;
    private static string? First(JsonElement el, params string[] names) { foreach (var n in names) { var v = Prop(el, n); if (!string.IsNullOrEmpty(v)) return v; } return null; }

    private List<MediaItem> GetNeedingGeocode()
    {
        var items = new List<MediaItem>();
        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = "SELECT id, file_name, latitude, longitude FROM media WHERE latitude IS NOT NULL AND longitude IS NOT NULL AND (city IS NULL OR city='') ORDER BY id";
        using var r = cmd.ExecuteReader();
        while (r.Read()) items.Add(new MediaItem { Id = r.GetInt64(0), FileName = r.GetString(1), Latitude = r.GetDouble(2), Longitude = r.GetDouble(3) });
        return items;
    }

    private void UpdateLocation(long id, string city, string country, string address)
    {
        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = "UPDATE media SET city=@c, country=@co, address=@a WHERE id=@id";
        cmd.Parameters.AddWithValue("@c", city); cmd.Parameters.AddWithValue("@co", country); cmd.Parameters.AddWithValue("@a", address); cmd.Parameters.AddWithValue("@id", id); cmd.ExecuteNonQuery();
    }
}

public class GeoResult { public string City { get; set; } = ""; public string Country { get; set; } = ""; public string State { get; set; } = ""; public string Address { get; set; } = ""; }
