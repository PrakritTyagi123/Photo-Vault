using PhotoVault.Core.Data;
using PhotoVault.Core.Models;

namespace PhotoVault.Services;

public class TripDetectionService
{
    private readonly DatabaseService _db;
    private readonly LogService _log;
    public TripDetectionService(DatabaseService db, LogService log) { _db = db; _log = log; }

    public List<DetectedTrip> DetectTrips()
    {
        var items = GetGpsItems();
        if (items.Count < 3) return new();
        var trips = new List<DetectedTrip>();
        var current = new List<MediaItem> { items[0] };
        for (int i = 1; i < items.Count; i++)
        {
            var timeDiff = (items[i].DisplayDate - items[i - 1].DisplayDate).TotalHours;
            var dist = Haversine(items[i - 1].Latitude ?? 0, items[i - 1].Longitude ?? 0, items[i].Latitude ?? 0, items[i].Longitude ?? 0);
            if (timeDiff > 24 || dist > 50) { if (current.Count >= 3) trips.Add(MakeTrip(current)); current = new(); }
            current.Add(items[i]);
        }
        if (current.Count >= 3) trips.Add(MakeTrip(current));
        _log.Info("Trips", $"Detected {trips.Count} trips"); return trips;
    }

    public int SaveTrips(List<DetectedTrip> trips)
    {
        EnsureTable(); int saved = 0;
        foreach (var trip in trips)
        {
            using var chk = _db.Connection.CreateCommand(); chk.CommandText = "SELECT COUNT(*) FROM trips WHERE name=@n"; chk.Parameters.AddWithValue("@n", trip.Name);
            if (Convert.ToInt32(chk.ExecuteScalar()) > 0) continue;
            using var cmd = _db.Connection.CreateCommand();
            cmd.CommandText = "INSERT INTO trips (name,start_date,end_date,country,city,photo_count) VALUES(@n,@s,@e,@co,@ci,@c); SELECT last_insert_rowid();";
            cmd.Parameters.AddWithValue("@n", trip.Name); cmd.Parameters.AddWithValue("@s", trip.StartDate.ToString("o")); cmd.Parameters.AddWithValue("@e", trip.EndDate.ToString("o"));
            cmd.Parameters.AddWithValue("@co", (object?)trip.Country ?? DBNull.Value); cmd.Parameters.AddWithValue("@ci", (object?)trip.City ?? DBNull.Value); cmd.Parameters.AddWithValue("@c", trip.PhotoCount);
            var tid = Convert.ToInt64(cmd.ExecuteScalar());
            foreach (var item in trip.Items) { using var lnk = _db.Connection.CreateCommand(); lnk.CommandText = "INSERT OR IGNORE INTO trip_media(trip_id,media_id) VALUES(@t,@m)"; lnk.Parameters.AddWithValue("@t", tid); lnk.Parameters.AddWithValue("@m", item.Id); lnk.ExecuteNonQuery(); }
            saved++;
        }
        _log.Info("Trips", $"Saved {saved} new trips"); return saved;
    }

    public List<TripInfo> GetAllTrips()
    {
        EnsureTable(); var trips = new List<TripInfo>();
        try
        {
            using var cmd = _db.Connection.CreateCommand(); cmd.CommandText = "SELECT id,name,start_date,end_date,country,city,photo_count FROM trips ORDER BY start_date DESC";
            using var r = cmd.ExecuteReader();
            while (r.Read()) trips.Add(new TripInfo { Id = r.GetInt64(0), Name = r.GetString(1), StartDate = DateTime.TryParse(r.GetString(2), out var s) ? s : DateTime.MinValue, EndDate = DateTime.TryParse(r.GetString(3), out var e) ? e : DateTime.MinValue, Country = r.IsDBNull(4) ? "" : r.GetString(4), City = r.IsDBNull(5) ? "" : r.GetString(5), PhotoCount = r.IsDBNull(6) ? 0 : r.GetInt32(6) });
        } catch { }
        return trips;
    }

    private void EnsureTable()
    {
        try
        {
            using var cmd = _db.Connection.CreateCommand();
            cmd.CommandText = "CREATE TABLE IF NOT EXISTS trips (id INTEGER PRIMARY KEY AUTOINCREMENT, name TEXT NOT NULL, start_date TEXT, end_date TEXT, country TEXT, city TEXT, photo_count INTEGER DEFAULT 0)";
            cmd.ExecuteNonQuery();
            using var cmd2 = _db.Connection.CreateCommand();
            cmd2.CommandText = "CREATE TABLE IF NOT EXISTS trip_media (trip_id INTEGER, media_id INTEGER, PRIMARY KEY(trip_id,media_id))";
            cmd2.ExecuteNonQuery();
        } catch { }
    }

    private DetectedTrip MakeTrip(List<MediaItem> items)
    {
        var sorted = items.OrderBy(i => i.DisplayDate).ToList();
        var city = items.FirstOrDefault(i => !string.IsNullOrEmpty(i.City))?.City;
        var country = items.FirstOrDefault(i => !string.IsNullOrEmpty(i.Country))?.Country;
        var name = !string.IsNullOrEmpty(city) ? $"{city} Trip" : !string.IsNullOrEmpty(country) ? $"{country} Trip" : $"Trip {sorted.First().DisplayDate:MMM yyyy}";
        var days = (sorted.Last().DisplayDate - sorted.First().DisplayDate).TotalDays;
        if (days > 1) name += $" ({(int)days + 1} days)";
        return new DetectedTrip { Name = name, StartDate = sorted.First().DisplayDate, EndDate = sorted.Last().DisplayDate, City = city, Country = country, Items = items, PhotoCount = items.Count };
    }

    private List<MediaItem> GetGpsItems()
    {
        var items = new List<MediaItem>(); using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = "SELECT id,file_path,file_name,latitude,longitude,date_taken,date_imported,city,country FROM media WHERE latitude IS NOT NULL AND longitude IS NOT NULL ORDER BY COALESCE(date_taken,date_imported)";
        using var r = cmd.ExecuteReader();
        while (r.Read()) items.Add(new MediaItem { Id = r.GetInt64(0), FilePath = r.GetString(1), FileName = r.GetString(2), Latitude = r.GetDouble(3), Longitude = r.GetDouble(4), DateTaken = r.IsDBNull(5) ? null : (DateTime.TryParse(r.GetString(5), out var d) ? d : null), DateImported = DateTime.TryParse(r.GetString(6), out var di) ? di : DateTime.UtcNow, City = r.IsDBNull(7) ? null : r.GetString(7), Country = r.IsDBNull(8) ? null : r.GetString(8) });
        return items;
    }

    private static double Haversine(double lat1, double lon1, double lat2, double lon2)
    {
        var dLat = (lat2 - lat1) * Math.PI / 180; var dLon = (lon2 - lon1) * Math.PI / 180;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) + Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return 6371 * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }
}

public class DetectedTrip { public string Name { get; set; } = ""; public DateTime StartDate { get; set; } public DateTime EndDate { get; set; } public string? City { get; set; } public string? Country { get; set; } public List<MediaItem> Items { get; set; } = new(); public int PhotoCount { get; set; } }
public class TripInfo { public long Id { get; set; } public string Name { get; set; } = ""; public DateTime StartDate { get; set; } public DateTime EndDate { get; set; } public string Country { get; set; } = ""; public string City { get; set; } = ""; public int PhotoCount { get; set; } public string DateRange => StartDate.Date == EndDate.Date ? StartDate.ToString("dd MMM yyyy") : $"{StartDate:dd MMM} — {EndDate:dd MMM yyyy}"; }
