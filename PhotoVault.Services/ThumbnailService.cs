using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Webp;
using PhotoVault.Core.Data;
using PhotoVault.Core.Models;

namespace PhotoVault.Services;

public class ThumbnailService
{
    private readonly DatabaseService _db;
    private readonly string _thumbDir;

    public ThumbnailService(DatabaseService db, string thumbDir) { _db = db; _thumbDir = thumbDir; }

    public Task GenerateAllAsync(IProgress<(int done, int total, string status)>? progress = null, CancellationToken ct = default)
    {
        var items = GetItemsNeedingThumbnails();
        int done = 0;
        foreach (var item in items)
        {
            if (ct.IsCancellationRequested) break;
            try { GenerateOne(item); } catch { }
            done++;
            progress?.Report((done, items.Count, $"Thumbnail {done}/{items.Count}"));
        }
        return Task.CompletedTask;
    }

    private void GenerateOne(MediaItem item)
    {
        if (!File.Exists(item.FilePath)) return;

        string? small = null, med = null, large = null;
        int? w = null, h = null;

        foreach (var size in new[] { 150, 400, 1080 })
        {
            var dir = Path.Combine(_thumbDir, size.ToString());
            System.IO.Directory.CreateDirectory(dir);
            var outPath = Path.Combine(dir, $"{item.Id}_{size}.webp");

            using var img = Image.Load(item.FilePath);
            if (w == null) { w = img.Width; h = img.Height; }

            img.Mutate(ctx => ctx.Resize(new ResizeOptions { Size = new Size(size, size), Mode = ResizeMode.Max }));
            img.Save(outPath, new WebpEncoder { Quality = 80 });

            switch (size) { case 150: small = outPath; break; case 400: med = outPath; break; case 1080: large = outPath; break; }
        }

        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = @"UPDATE media SET has_thumbnail=1, thumbnail_small=@s, thumbnail_medium=@m, thumbnail_large=@l,
            width=COALESCE(@w,width), height=COALESCE(@h,height) WHERE id=@id";
        cmd.Parameters.AddWithValue("@s", (object?)small ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@m", (object?)med ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@l", (object?)large ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@w", w.HasValue ? w.Value : (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@h", h.HasValue ? h.Value : (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@id", item.Id);
        cmd.ExecuteNonQuery();
    }

    private List<MediaItem> GetItemsNeedingThumbnails()
    {
        var items = new List<MediaItem>();
        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = "SELECT id, file_path, file_name FROM media WHERE has_thumbnail=0 AND media_type NOT IN ('Video','SlowMotion') ORDER BY id";
        using var r = cmd.ExecuteReader();
        while (r.Read()) items.Add(new MediaItem { Id = r.GetInt64(0), FilePath = r.GetString(1), FileName = r.GetString(2) });
        return items;
    }
}
