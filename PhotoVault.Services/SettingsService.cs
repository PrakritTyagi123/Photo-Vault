using PhotoVault.Core.Data;

namespace PhotoVault.Services;

public class SettingsService
{
    private readonly DatabaseService _db;
    private readonly LogService _log;
    public SettingsService(DatabaseService db, LogService log) { _db = db; _log = log; }

    public string? Get(string key) { using var cmd = _db.Connection.CreateCommand(); cmd.CommandText = "SELECT value FROM settings WHERE key=@k"; cmd.Parameters.AddWithValue("@k", key); var r = cmd.ExecuteScalar(); return r != null && r != DBNull.Value ? r.ToString() : null; }
    public void Set(string key, string value) { using var cmd = _db.Connection.CreateCommand(); cmd.CommandText = "INSERT OR REPLACE INTO settings (key,value) VALUES(@k,@v)"; cmd.Parameters.AddWithValue("@k", key); cmd.Parameters.AddWithValue("@v", value); cmd.ExecuteNonQuery(); }
    public bool GetBool(string key) => Get(key) == "true";
    public void SetBool(string key, bool val) => Set(key, val ? "true" : "false");
    public int GetInt(string key, int def = 0) { var v = Get(key); return int.TryParse(v, out var i) ? i : def; }
    public void SetInt(string key, int val) => Set(key, val.ToString());

    public void EnsureDefaults()
    {
        if (Get("auto_scan_startup") == null) SetBool("auto_scan_startup", false);
        if (Get("auto_detect_drives") == null) SetBool("auto_detect_drives", true);
        if (Get("sha256_tracking") == null) SetBool("sha256_tracking", true);
        if (Get("max_concurrent_tasks") == null) SetInt("max_concurrent_tasks", 4);
        if (Get("ignore_patterns") == null) Set("ignore_patterns", "node_modules,.cache,temp,.git,.DS_Store,Thumbs.db");
        if (Get("inference_backend") == null) Set("inference_backend", "ONNX Runtime + CUDA");
    }

    public void AddWatchedFolder(string path)
    {
        long fileCount = 0; long totalSize = 0;
        if (Directory.Exists(path))
        {
            var files = Directory.EnumerateFiles(path, "*.*", SearchOption.AllDirectories).ToList();
            fileCount = files.Count; totalSize = files.Sum(f => { try { return new FileInfo(f).Length; } catch { return 0L; } });
        }
        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = "INSERT OR IGNORE INTO watched_folders (path,date_added,file_count,total_size) VALUES(@p,@d,@fc,@ts)";
        cmd.Parameters.AddWithValue("@p", path); cmd.Parameters.AddWithValue("@d", DateTime.UtcNow.ToString("o"));
        cmd.Parameters.AddWithValue("@fc", fileCount); cmd.Parameters.AddWithValue("@ts", totalSize);
        cmd.ExecuteNonQuery();
        _log.Info("Settings", $"Added watched folder: {path}");
    }

    public void RemoveWatchedFolder(long id) { using var cmd = _db.Connection.CreateCommand(); cmd.CommandText = "DELETE FROM watched_folders WHERE id=@id"; cmd.Parameters.AddWithValue("@id", id); cmd.ExecuteNonQuery(); }
    public void SetFolderActive(long id, bool active) { using var cmd = _db.Connection.CreateCommand(); cmd.CommandText = "UPDATE watched_folders SET is_active=@a WHERE id=@id"; cmd.Parameters.AddWithValue("@a", active ? 1 : 0); cmd.Parameters.AddWithValue("@id", id); cmd.ExecuteNonQuery(); }

    public List<WatchedFolderInfo> GetWatchedFolders()
    {
        var list = new List<WatchedFolderInfo>();
        using var cmd = _db.Connection.CreateCommand(); cmd.CommandText = "SELECT * FROM watched_folders ORDER BY date_added DESC";
        using var r = cmd.ExecuteReader();
        while (r.Read()) list.Add(new WatchedFolderInfo { Id = r.GetInt64(0), Path = r.GetString(1), IsActive = r.GetInt32(2) == 1, FileCount = r.IsDBNull(4) ? 0 : r.GetInt32(4), TotalSize = r.IsDBNull(5) ? 0 : r.GetInt64(5) });
        return list;
    }

    public LibraryStats GetLibraryStats()
    {
        var stats = new LibraryStats();
        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = @"SELECT COALESCE(COUNT(*),0), COALESCE(SUM(file_size),0),
            COALESCE(SUM(CASE WHEN has_thumbnail=1 THEN 1 ELSE 0 END),0),
            COALESCE(SUM(CASE WHEN media_type='Photo' THEN 1 ELSE 0 END),0),
            COALESCE(SUM(CASE WHEN media_type='Video' THEN 1 ELSE 0 END),0) FROM media";
        using var r = cmd.ExecuteReader();
        if (r.Read()) { stats.TotalItems = r.GetInt32(0); stats.TotalSize = r.GetInt64(1); stats.ThumbnailCount = r.GetInt32(2); stats.PhotoCount = r.GetInt32(3); stats.VideoCount = r.GetInt32(4); }
        return stats;
    }
}

public class WatchedFolderInfo
{
    public long Id { get; set; } public string Path { get; set; } = ""; public bool IsActive { get; set; } = true;
    public int FileCount { get; set; } public long TotalSize { get; set; }
    public string InfoText => $"{FileCount} files — {(TotalSize > 1024L * 1024 * 1024 ? $"{TotalSize / (1024.0 * 1024 * 1024):N1} GB" : $"{TotalSize / (1024.0 * 1024):N0} MB")}";
}

public class LibraryStats
{
    public int TotalItems { get; set; } public long TotalSize { get; set; } public int ThumbnailCount { get; set; }
    public int PhotoCount { get; set; } public int VideoCount { get; set; }
    public string TotalSizeDisplay => TotalSize > 1024L * 1024 * 1024 ? $"{TotalSize / (1024.0 * 1024 * 1024):N2} GB" : $"{TotalSize / (1024.0 * 1024):N0} MB";
}
