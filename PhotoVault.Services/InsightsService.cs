using PhotoVault.Core.Data;

namespace PhotoVault.Services;

public class InsightsService
{
    private readonly DatabaseService _db;
    public InsightsService(DatabaseService db) { _db = db; }

    public List<(string camera, int count)> GetTopCameras(int limit = 10)
    {
        var r = new List<(string, int)>(); using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = $"SELECT camera_model, COUNT(*) as c FROM media WHERE camera_model IS NOT NULL AND camera_model!='' GROUP BY camera_model ORDER BY c DESC LIMIT {limit}";
        using var rd = cmd.ExecuteReader(); while (rd.Read()) r.Add((rd.GetString(0), rd.GetInt32(1))); return r;
    }

    public List<(string location, int count)> GetTopLocations(int limit = 10)
    {
        var r = new List<(string, int)>(); using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = $"SELECT city||', '||country, COUNT(*) as c FROM media WHERE city IS NOT NULL AND city!='' GROUP BY city,country ORDER BY c DESC LIMIT {limit}";
        using var rd = cmd.ExecuteReader(); while (rd.Read()) r.Add((rd.GetString(0), rd.GetInt32(1))); return r;
    }

    public List<(string type, int count, long size)> GetStorageBreakdown()
    {
        var r = new List<(string, int, long)>(); using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = "SELECT media_type, COUNT(*), COALESCE(SUM(file_size),0) FROM media GROUP BY media_type ORDER BY COUNT(*) DESC";
        using var rd = cmd.ExecuteReader(); while (rd.Read()) r.Add((rd.IsDBNull(0) ? "Unknown" : rd.GetString(0), rd.GetInt32(1), rd.GetInt64(2))); return r;
    }

    public List<(string ext, int count)> GetTopFormats(int limit = 10)
    {
        var r = new List<(string, int)>(); using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = $"SELECT UPPER(file_extension), COUNT(*) as c FROM media GROUP BY file_extension ORDER BY c DESC LIMIT {limit}";
        using var rd = cmd.ExecuteReader(); while (rd.Read()) r.Add((rd.GetString(0).TrimStart('.'), rd.GetInt32(1))); return r;
    }

    public (double avg, double min, double max) GetQualityStats()
    {
        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = "SELECT COALESCE(AVG(quality_score),0), COALESCE(MIN(quality_score),0), COALESCE(MAX(quality_score),0) FROM media WHERE quality_score IS NOT NULL";
        using var r = cmd.ExecuteReader(); if (r.Read()) return (r.GetDouble(0), r.GetDouble(1), r.GetDouble(2)); return (0, 0, 0);
    }

    public int GetGpsCount() { using var cmd = _db.Connection.CreateCommand(); cmd.CommandText = "SELECT COUNT(*) FROM media WHERE latitude IS NOT NULL"; return Convert.ToInt32(cmd.ExecuteScalar()); }
    public int GetFavoriteCount() { using var cmd = _db.Connection.CreateCommand(); cmd.CommandText = "SELECT COUNT(*) FROM media WHERE is_favorite=1"; return Convert.ToInt32(cmd.ExecuteScalar()); }
}
