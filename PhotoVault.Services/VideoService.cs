using System.IO;
using System.Text.RegularExpressions;
using PhotoVault.Core.Data;
using PhotoVault.Core.Models;
using Xabe.FFmpeg;
using Xabe.FFmpeg.Downloader;

namespace PhotoVault.Services;

public class VideoService
{
    private readonly DatabaseService _db;
    private readonly string _thumbnailDir;
    private readonly LogService _log;
    private bool _ffmpegReady;

    public VideoService(DatabaseService db, string thumbnailDir, LogService log) { _db = db; _thumbnailDir = thumbnailDir; _log = log; }

    public async Task EnsureFFmpegAsync()
    {
        if (_ffmpegReady) return;
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PhotoVault", "ffmpeg");
        Directory.CreateDirectory(dir);
        FFmpeg.SetExecutablesPath(dir);
        if (!File.Exists(Path.Combine(dir, "ffmpeg.exe")))
        {
            _log.Info("VideoService", "Downloading FFmpeg (first run)...");
            await FFmpegDownloader.GetLatestVersion(FFmpegVersion.Official, dir);
            _log.Info("VideoService", "FFmpeg ready");
        }
        _ffmpegReady = true;
    }

    public async Task<int> ExtractAllMetadataAsync(IProgress<(int done, int total)>? progress = null, CancellationToken ct = default)
    {
        await EnsureFFmpegAsync();
        var items = GetVideos("has_exif=0");
        int done = 0, ok = 0;
        foreach (var item in items)
        {
            if (ct.IsCancellationRequested) break;
            if (await ExtractMetadataAsync(item)) ok++;
            done++; progress?.Report((done, items.Count));
        }
        _log.Info("VideoService", $"Metadata: {ok}/{items.Count}");
        return ok;
    }

    public async Task<int> GenerateAllThumbnailsAsync(IProgress<(int done, int total)>? progress = null, CancellationToken ct = default)
    {
        await EnsureFFmpegAsync();
        var items = GetVideos("has_thumbnail=0");
        int done = 0, ok = 0;
        foreach (var item in items)
        {
            if (ct.IsCancellationRequested) break;
            if (await GenerateThumbnailAsync(item)) ok++;
            done++; progress?.Report((done, items.Count));
        }
        _log.Info("VideoService", $"Thumbnails: {ok}/{items.Count}");
        return ok;
    }

    private async Task<bool> ExtractMetadataAsync(MediaItem item)
    {
        if (!File.Exists(item.FilePath)) return false;
        try
        {
            var info = await FFmpeg.GetMediaInfo(item.FilePath);
            var vs = info.VideoStreams.FirstOrDefault();
            int? w = vs?.Width, h = vs?.Height;

            DateTime? dateTaken = null; double? lat = null, lon = null, alt = null; string? camera = null;
            try
            {
                var dirs = MetadataExtractor.ImageMetadataReader.ReadMetadata(item.FilePath);
                foreach (var dir in dirs)
                {
                    if (dateTaken == null)
                    {
                        var dtag = dir.Tags.FirstOrDefault(t => t.Name.Contains("Creation Date", StringComparison.OrdinalIgnoreCase) || t.Name.Contains("Date/Time Original", StringComparison.OrdinalIgnoreCase));
                        if (dtag != null && DateTime.TryParse(dtag.Description, out var dt)) dateTaken = dt;
                    }
                    if (camera == null)
                    {
                        var mtag = dir.Tags.FirstOrDefault(t => t.Name.Contains("Model", StringComparison.OrdinalIgnoreCase) && !t.Name.Contains("Handler", StringComparison.OrdinalIgnoreCase));
                        if (mtag != null && !string.IsNullOrEmpty(mtag.Description)) camera = mtag.Description;
                    }
                }
                var qDir = dirs.OfType<MetadataExtractor.Formats.QuickTime.QuickTimeMetadataHeaderDirectory>().FirstOrDefault();
                if (qDir != null)
                {
                    var locTag = qDir.Tags.FirstOrDefault(t => t.Name.Contains("GPS Location", StringComparison.OrdinalIgnoreCase));
                    if (locTag?.Description != null)
                    {
                        var parts = Regex.Matches(locTag.Description.TrimEnd('/'), @"[+-][\d.]+");
                        if (parts.Count >= 2) { double.TryParse(parts[0].Value, System.Globalization.CultureInfo.InvariantCulture, out var la); lat = la; double.TryParse(parts[1].Value, System.Globalization.CultureInfo.InvariantCulture, out var lo); lon = lo; }
                        if (parts.Count >= 3) { double.TryParse(parts[2].Value, System.Globalization.CultureInfo.InvariantCulture, out var a); alt = a; }
                    }
                }
                var stdGps = dirs.OfType<MetadataExtractor.Formats.Exif.GpsDirectory>().FirstOrDefault();
                if (stdGps != null && lat == null) { var geo = stdGps.GetGeoLocation(); if (geo != null) { lat = geo.Value.Latitude; lon = geo.Value.Longitude; } }
            }
            catch { }

            if (dateTaken == null) dateTaken = new FileInfo(item.FilePath).LastWriteTime;

            using var cmd = _db.Connection.CreateCommand();
            cmd.CommandText = @"UPDATE media SET has_exif=1, width=@w, height=@h, date_taken=@dt, latitude=@lat, longitude=@lon, altitude=@alt, camera_model=@cam WHERE id=@id";
            cmd.Parameters.AddWithValue("@w", w.HasValue ? w.Value : (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@h", h.HasValue ? h.Value : (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@dt", dateTaken.HasValue ? dateTaken.Value.ToString("o") : (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@lat", lat.HasValue ? lat.Value : (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@lon", lon.HasValue ? lon.Value : (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@alt", alt.HasValue ? alt.Value : (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@cam", (object?)camera ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@id", item.Id);
            cmd.ExecuteNonQuery();
            return true;
        }
        catch (Exception ex) { _log.Error("VideoService", $"Meta fail {item.FileName}: {ex.Message}"); return false; }
    }

    private async Task<bool> GenerateThumbnailAsync(MediaItem item)
    {
        if (!File.Exists(item.FilePath)) return false;
        try
        {
            var info = await FFmpeg.GetMediaInfo(item.FilePath);
            var dur = info.Duration;
            var captureTime = dur.TotalSeconds > 10 ? TimeSpan.FromSeconds(dur.TotalSeconds * 0.1) : TimeSpan.FromSeconds(Math.Min(1, dur.TotalSeconds * 0.5));

            string? small = null, med = null, large = null;
            foreach (var size in new[] { 150, 400, 1080 })
            {
                var dir = Path.Combine(_thumbnailDir, size.ToString());
                Directory.CreateDirectory(dir);
                var path = Path.Combine(dir, $"{item.Id}_{size}.png");
                var conv = await FFmpeg.Conversions.FromSnippet.Snapshot(item.FilePath, path, captureTime);
                conv.SetOverwriteOutput(true);
                await conv.Start();
                if (File.Exists(path)) { switch (size) { case 150: small = path; break; case 400: med = path; break; case 1080: large = path; break; } }
            }

            if (small != null)
            {
                using var cmd = _db.Connection.CreateCommand();
                cmd.CommandText = "UPDATE media SET has_thumbnail=1, thumbnail_small=@s, thumbnail_medium=@m, thumbnail_large=@l WHERE id=@id";
                cmd.Parameters.AddWithValue("@s", (object?)small ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@m", (object?)(med ?? small) ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@l", (object?)(large ?? med ?? small) ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@id", item.Id);
                cmd.ExecuteNonQuery();
                return true;
            }
            return false;
        }
        catch (Exception ex) { _log.Error("VideoService", $"Thumb fail {item.FileName}: {ex.Message}"); return false; }
    }

    private List<MediaItem> GetVideos(string where)
    {
        var items = new List<MediaItem>();
        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = $"SELECT id, file_path, file_name FROM media WHERE {where} AND media_type IN ('Video','SlowMotion') ORDER BY id";
        using var r = cmd.ExecuteReader();
        while (r.Read()) items.Add(new MediaItem { Id = r.GetInt64(0), FilePath = r.GetString(1), FileName = r.GetString(2) });
        return items;
    }
}
