using PhotoVault.Core.Data;
using PhotoVault.Core.Models;

namespace PhotoVault.Services;

public class HealthService
{
    private readonly DatabaseService _db;
    private readonly LogService _log;
    public HealthService(DatabaseService db, LogService log) { _db = db; _log = log; }

    public HealthReport GenerateReport()
    {
        var r = new HealthReport();
        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM media"; r.TotalItems = Convert.ToInt32(cmd.ExecuteScalar());
        cmd.CommandText = "SELECT COUNT(*) FROM media WHERE has_thumbnail=0"; r.MissingThumbnails = Convert.ToInt32(cmd.ExecuteScalar());
        cmd.CommandText = "SELECT COUNT(*) FROM media WHERE has_exif=0"; r.MissingExif = Convert.ToInt32(cmd.ExecuteScalar());
        cmd.CommandText = "SELECT COUNT(*) FROM media WHERE date_taken IS NULL"; r.MissingDateTaken = Convert.ToInt32(cmd.ExecuteScalar());
        cmd.CommandText = "SELECT COUNT(*) FROM media WHERE latitude IS NULL AND has_exif=1"; r.MissingGps = Convert.ToInt32(cmd.ExecuteScalar());
        cmd.CommandText = "SELECT COUNT(*) FROM media WHERE latitude IS NOT NULL AND (city IS NULL OR city='')"; r.MissingGeocode = Convert.ToInt32(cmd.ExecuteScalar());
        cmd.CommandText = "SELECT COUNT(*) FROM media WHERE quality_score IS NULL AND media_type NOT IN ('Video','SlowMotion')"; r.MissingQuality = Convert.ToInt32(cmd.ExecuteScalar());
        cmd.CommandText = "SELECT COUNT(*) FROM media WHERE (file_hash IS NULL OR file_hash='')"; r.MissingHash = Convert.ToInt32(cmd.ExecuteScalar());
        cmd.CommandText = "SELECT COUNT(*) FROM media WHERE has_tags=0"; r.MissingTags = Convert.ToInt32(cmd.ExecuteScalar());
        cmd.CommandText = "SELECT COUNT(*) FROM media WHERE has_faces=0 AND media_type NOT IN ('Video','SlowMotion')"; r.MissingFaces = Convert.ToInt32(cmd.ExecuteScalar());
        r.BrokenFiles = CountBroken();
        if (r.TotalItems > 0) r.HealthScore = (int)(((r.TotalItems - r.MissingThumbnails) + (r.TotalItems - r.MissingExif) + (r.TotalItems - r.MissingHash)) * 100.0 / (r.TotalItems * 3));
        return r;
    }

    public int RemoveBrokenEntries()
    {
        var broken = GetBrokenFiles();
        foreach (var item in broken) { using var cmd = _db.Connection.CreateCommand(); cmd.CommandText = "DELETE FROM media WHERE id=@id"; cmd.Parameters.AddWithValue("@id", item.Id); cmd.ExecuteNonQuery(); }
        _log.Info("Health", $"Removed {broken.Count} broken entries"); return broken.Count;
    }

    public List<MediaItem> GetBrokenFiles()
    {
        var list = new List<MediaItem>(); using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = "SELECT id, file_path, file_name, file_extension, file_size FROM media ORDER BY id";
        using var r = cmd.ExecuteReader();
        while (r.Read()) { if (!File.Exists(r.GetString(1))) list.Add(new MediaItem { Id = r.GetInt64(0), FilePath = r.GetString(1), FileName = r.GetString(2), FileExtension = r.GetString(3), FileSize = r.GetInt64(4) }); }
        return list;
    }

    private int CountBroken() { int c = 0; using var cmd = _db.Connection.CreateCommand(); cmd.CommandText = "SELECT file_path FROM media"; using var r = cmd.ExecuteReader(); while (r.Read()) if (!File.Exists(r.GetString(0))) c++; return c; }
}

public class HealthReport
{
    public int TotalItems { get; set; } public int MissingThumbnails { get; set; } public int MissingExif { get; set; }
    public int MissingDateTaken { get; set; } public int MissingGps { get; set; } public int MissingGeocode { get; set; }
    public int MissingQuality { get; set; } public int MissingHash { get; set; } public int MissingTags { get; set; }
    public int MissingFaces { get; set; } public int BrokenFiles { get; set; } public int HealthScore { get; set; }
}
