using System.Security.Cryptography;
using PhotoVault.Core.Data;
using PhotoVault.Core.Models;

namespace PhotoVault.Services;

public class DuplicateDetectionService
{
    private readonly DatabaseService _db;
    private readonly LogService _log;
    public DuplicateDetectionService(DatabaseService db, LogService log) { _db = db; _log = log; }

    public Task<int> ComputeHashesAsync(IProgress<(int done, int total)>? progress = null, CancellationToken ct = default)
    {
        var items = GetWithoutHash(); int done = 0, ok = 0;
        foreach (var item in items)
        {
            if (ct.IsCancellationRequested) break;
            try
            {
                if (File.Exists(item.FilePath))
                {
                    using var stream = File.OpenRead(item.FilePath);
                    var hash = BitConverter.ToString(SHA256.HashData(stream)).Replace("-", "").ToLowerInvariant();
                    using var cmd = _db.Connection.CreateCommand(); cmd.CommandText = "UPDATE media SET file_hash=@h WHERE id=@id"; cmd.Parameters.AddWithValue("@h", hash); cmd.Parameters.AddWithValue("@id", item.Id); cmd.ExecuteNonQuery();
                    ok++;
                }
            } catch { }
            done++; progress?.Report((done, items.Count));
        }
        _log.Info("DuplicateDetection", $"Hashed {ok}/{items.Count}"); return Task.FromResult(ok);
    }

    public List<DuplicateGroup> FindDuplicates()
    {
        var groups = new List<DuplicateGroup>();
        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = "SELECT file_hash, COUNT(*) as c FROM media WHERE file_hash IS NOT NULL AND file_hash!='' GROUP BY file_hash HAVING c>1 ORDER BY c DESC";
        var hashes = new List<string>();
        using var r = cmd.ExecuteReader(); while (r.Read()) hashes.Add(r.GetString(0));
        foreach (var hash in hashes)
        {
            var items = GetByHash(hash);
            if (items.Count > 1) groups.Add(new DuplicateGroup { Hash = hash, Items = items, Count = items.Count, TotalSize = items.Sum(i => i.FileSize), WastedSize = items.Sum(i => i.FileSize) - items.Max(i => i.FileSize) });
        }
        return groups;
    }

    public int GetTotalDuplicateCount()
    {
        try { using var cmd = _db.Connection.CreateCommand(); cmd.CommandText = "SELECT COALESCE(SUM(c-1),0) FROM (SELECT COUNT(*) as c FROM media WHERE file_hash IS NOT NULL AND file_hash!='' GROUP BY file_hash HAVING c>1)"; return Convert.ToInt32(cmd.ExecuteScalar()); } catch { return 0; }
    }

    private List<MediaItem> GetWithoutHash()
    {
        var items = new List<MediaItem>(); using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = "SELECT id, file_path, file_name FROM media WHERE (file_hash IS NULL OR file_hash='') ORDER BY id";
        using var r = cmd.ExecuteReader(); while (r.Read()) items.Add(new MediaItem { Id = r.GetInt64(0), FilePath = r.GetString(1), FileName = r.GetString(2) }); return items;
    }

    private List<MediaItem> GetByHash(string hash)
    {
        var items = new List<MediaItem>(); using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = "SELECT id, file_path, file_name, file_extension, file_size, media_type, thumbnail_small, has_thumbnail FROM media WHERE file_hash=@h ORDER BY file_size DESC";
        cmd.Parameters.AddWithValue("@h", hash);
        using var r = cmd.ExecuteReader();
        while (r.Read()) { Enum.TryParse<MediaType>(r.IsDBNull(5) ? "Photo" : r.GetString(5), out var mt); items.Add(new MediaItem { Id = r.GetInt64(0), FilePath = r.GetString(1), FileName = r.GetString(2), FileExtension = r.GetString(3), FileSize = r.GetInt64(4), MediaType = mt, ThumbnailSmall = r.IsDBNull(6) ? null : r.GetString(6), HasThumbnail = !r.IsDBNull(7) && r.GetInt32(7) == 1 }); }
        return items;
    }
}

public class DuplicateGroup
{
    public string Hash { get; set; } = ""; public List<MediaItem> Items { get; set; } = new(); public int Count { get; set; }
    public long TotalSize { get; set; } public long WastedSize { get; set; }
    public string WastedSizeDisplay => WastedSize > 1024 * 1024 ? $"{WastedSize / (1024.0 * 1024):N1} MB" : $"{WastedSize / 1024.0:N0} KB";
}
