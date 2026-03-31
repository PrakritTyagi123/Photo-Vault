using PhotoVault.Core.Data;
using PhotoVault.Core.Models;

namespace PhotoVault.Services;

public class CleanupService
{
    private readonly DatabaseService _db;
    private readonly DuplicateDetectionService _dup;
    private readonly LogService _log;
    public CleanupService(DatabaseService db, DuplicateDetectionService dup, LogService log) { _db = db; _dup = dup; _log = log; }

    public List<DuplicateGroup> GetDuplicates() => _dup.FindDuplicates();

    public List<MediaItem> GetLargeFiles(long min = 50 * 1024 * 1024)
    {
        var items = new List<MediaItem>(); using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = "SELECT id,file_path,file_name,file_extension,file_size,media_type,thumbnail_small,has_thumbnail FROM media WHERE file_size>=@m ORDER BY file_size DESC LIMIT 100";
        cmd.Parameters.AddWithValue("@m", min); using var r = cmd.ExecuteReader();
        while (r.Read()) { Enum.TryParse<MediaType>(r.IsDBNull(5) ? "Photo" : r.GetString(5), out var mt); items.Add(new MediaItem { Id = r.GetInt64(0), FilePath = r.GetString(1), FileName = r.GetString(2), FileExtension = r.GetString(3), FileSize = r.GetInt64(4), MediaType = mt, ThumbnailSmall = r.IsDBNull(6) ? null : r.GetString(6), HasThumbnail = !r.IsDBNull(7) && r.GetInt32(7) == 1 }); }
        return items;
    }

    public List<MediaItem> GetLowQuality(double max = 30)
    {
        var items = new List<MediaItem>(); using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = "SELECT id,file_path,file_name,file_extension,file_size,media_type,quality_score,thumbnail_small,has_thumbnail FROM media WHERE quality_score IS NOT NULL AND quality_score<=@m ORDER BY quality_score ASC LIMIT 100";
        cmd.Parameters.AddWithValue("@m", max); using var r = cmd.ExecuteReader();
        while (r.Read()) { Enum.TryParse<MediaType>(r.IsDBNull(5) ? "Photo" : r.GetString(5), out var mt); items.Add(new MediaItem { Id = r.GetInt64(0), FilePath = r.GetString(1), FileName = r.GetString(2), FileExtension = r.GetString(3), FileSize = r.GetInt64(4), MediaType = mt, QualityScore = r.IsDBNull(6) ? null : r.GetDouble(6), ThumbnailSmall = r.IsDBNull(7) ? null : r.GetString(7), HasThumbnail = !r.IsDBNull(8) && r.GetInt32(8) == 1 }); }
        return items;
    }

    public void DeleteMedia(long id, bool deleteFile = false)
    {
        if (deleteFile)
        {
            using var g = _db.Connection.CreateCommand(); g.CommandText = "SELECT file_path,thumbnail_small,thumbnail_medium,thumbnail_large FROM media WHERE id=@id"; g.Parameters.AddWithValue("@id", id);
            using var r = g.ExecuteReader(); if (r.Read()) { for (int i = 1; i <= 3; i++) if (!r.IsDBNull(i)) try { File.Delete(r.GetString(i)); } catch { } if (!r.IsDBNull(0)) try { File.Delete(r.GetString(0)); } catch { } }
        }
        using var c1 = _db.Connection.CreateCommand(); c1.CommandText = "DELETE FROM album_media WHERE media_id=@id"; c1.Parameters.AddWithValue("@id", id); c1.ExecuteNonQuery();
        using var c2 = _db.Connection.CreateCommand(); c2.CommandText = "DELETE FROM media WHERE id=@id"; c2.Parameters.AddWithValue("@id", id); c2.ExecuteNonQuery();
        _log.Info("Cleanup", $"Deleted media {id}");
    }

    public CleanupSummary GetSummary()
    {
        var s = new CleanupSummary(); s.DuplicateGroups = _dup.GetTotalDuplicateCount();
        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM media WHERE file_size>=52428800"; s.LargeFiles = Convert.ToInt32(cmd.ExecuteScalar());
        cmd.CommandText = "SELECT COUNT(*) FROM media WHERE quality_score IS NOT NULL AND quality_score<=30"; s.LowQualityFiles = Convert.ToInt32(cmd.ExecuteScalar());
        return s;
    }
}

public class CleanupSummary { public int DuplicateGroups { get; set; } public int LargeFiles { get; set; } public int LowQualityFiles { get; set; } }
