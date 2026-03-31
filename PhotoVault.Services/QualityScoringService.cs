using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using PhotoVault.Core.Data;
using PhotoVault.Core.Models;

namespace PhotoVault.Services;

public class QualityScoringService
{
    private readonly DatabaseService _db;
    private readonly LogService _log;
    public QualityScoringService(DatabaseService db, LogService log) { _db = db; _log = log; }

    public Task<int> ScoreAllAsync(IProgress<(int done, int total)>? progress = null, CancellationToken ct = default)
    {
        var items = GetUnscored(); int done = 0, ok = 0;
        foreach (var item in items)
        {
            if (ct.IsCancellationRequested) break;
            try
            {
                var score = Score(item);
                if (score >= 0) { UpdateScore(item.Id, score); ok++; }
            } catch { }
            done++; progress?.Report((done, items.Count));
        }
        _log.Info("Quality", $"Scored {ok}/{items.Count}"); return Task.FromResult(ok);
    }

    private double Score(MediaItem item)
    {
        if (!File.Exists(item.FilePath) || item.MediaType == MediaType.Video) return -1;
        var path = item.ThumbnailMedium ?? item.ThumbnailSmall ?? item.FilePath;
        if (!File.Exists(path)) path = item.FilePath;

        using var img = Image.Load<Rgba32>(path);
        if (img.Width > 400 || img.Height > 400) img.Mutate(c => c.Resize(new ResizeOptions { Size = new Size(400, 400), Mode = ResizeMode.Max }));

        double sharpness = LaplacianVariance(img);
        double sharpScore = Math.Min(60, sharpness / 8.0);
        long pixels = (long)(item.Width ?? img.Width) * (item.Height ?? img.Height);
        double resScore = pixels >= 12_000_000 ? 25 : pixels >= 8_000_000 ? 22 : pixels >= 4_000_000 ? 18 : pixels >= 2_000_000 ? 14 : pixels >= 1_000_000 ? 10 : Math.Max(2, pixels / 100_000.0);
        double bpp = pixels > 0 ? (double)item.FileSize / pixels : 0;
        double sizeScore = bpp >= 3 ? 15 : bpp >= 1.5 ? 12 : bpp >= 0.5 ? 9 : Math.Max(2, bpp * 18);
        return Math.Round(Math.Min(100, sharpScore + resScore + sizeScore));
    }

    private static double LaplacianVariance(Image<Rgba32> img)
    {
        int w = img.Width, h = img.Height; if (w < 3 || h < 3) return 0;
        double sum = 0, sumSq = 0; int count = 0;
        for (int y = 1; y < h - 1; y++)
            for (int x = 1; x < w - 1; x++)
            {
                double c = Lum(img[x, y]), lap = Lum(img[x, y - 1]) + Lum(img[x, y + 1]) + Lum(img[x - 1, y]) + Lum(img[x + 1, y]) - 4 * c;
                sum += lap; sumSq += lap * lap; count++;
            }
        if (count == 0) return 0;
        double mean = sum / count; return (sumSq / count) - (mean * mean);
    }

    private static double Lum(Rgba32 p) => 0.299 * p.R + 0.587 * p.G + 0.114 * p.B;

    private List<MediaItem> GetUnscored()
    {
        var items = new List<MediaItem>(); using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = "SELECT id, file_path, file_name, file_extension, file_size, media_type, width, height, thumbnail_small, thumbnail_medium FROM media WHERE quality_score IS NULL AND media_type NOT IN ('Video','SlowMotion') ORDER BY id";
        using var r = cmd.ExecuteReader();
        while (r.Read()) { Enum.TryParse<MediaType>(r.IsDBNull(5) ? "Photo" : r.GetString(5), out var mt); items.Add(new MediaItem { Id = r.GetInt64(0), FilePath = r.GetString(1), FileName = r.GetString(2), FileExtension = r.GetString(3), FileSize = r.GetInt64(4), MediaType = mt, Width = r.IsDBNull(6) ? null : r.GetInt32(6), Height = r.IsDBNull(7) ? null : r.GetInt32(7), ThumbnailSmall = r.IsDBNull(8) ? null : r.GetString(8), ThumbnailMedium = r.IsDBNull(9) ? null : r.GetString(9) }); }
        return items;
    }

    private void UpdateScore(long id, double score) { using var cmd = _db.Connection.CreateCommand(); cmd.CommandText = "UPDATE media SET quality_score=@s WHERE id=@id"; cmd.Parameters.AddWithValue("@s", score); cmd.Parameters.AddWithValue("@id", id); cmd.ExecuteNonQuery(); }
}
