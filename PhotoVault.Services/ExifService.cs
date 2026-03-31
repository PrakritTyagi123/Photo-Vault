using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using PhotoVault.Core.Data;
using PhotoVault.Core.Models;

namespace PhotoVault.Services;

public class ExifService
{
    private readonly DatabaseService _db;
    public ExifService(DatabaseService db) { _db = db; }

    public Task ExtractAllAsync(IProgress<(int done, int total)>? progress = null, CancellationToken ct = default)
    {
        var items = GetItemsNeedingExif();
        int done = 0;
        foreach (var item in items)
        {
            if (ct.IsCancellationRequested) break;
            try { ExtractOne(item); } catch { }
            done++;
            progress?.Report((done, items.Count));
        }
        return Task.CompletedTask;
    }

    private void ExtractOne(MediaItem item)
    {
        if (!File.Exists(item.FilePath)) return;
        var dirs = ImageMetadataReader.ReadMetadata(item.FilePath);

        string? camera = null, lens = null, aperture = null, shutter = null;
        int? iso = null, width = null, height = null, orientation = null;
        double? focal = null, lat = null, lon = null, alt = null;
        DateTime? dateTaken = null;

        foreach (var dir in dirs)
        {
            if (dir is ExifIfd0Directory ifd0)
            {
                camera = ifd0.GetDescription(ExifDirectoryBase.TagModel)?.Trim();
                if (ifd0.TryGetDateTime(ExifDirectoryBase.TagDateTime, out var dt)) dateTaken = dt;
                if (ifd0.TryGetInt32(ExifDirectoryBase.TagOrientation, out var o)) orientation = o;
            }
            if (dir is ExifSubIfdDirectory sub)
            {
                lens = sub.GetDescription(ExifDirectoryBase.TagLensModel)?.Trim();
                aperture = sub.GetDescription(ExifDirectoryBase.TagFNumber);
                shutter = sub.GetDescription(ExifDirectoryBase.TagExposureTime);
                if (sub.TryGetInt32(ExifDirectoryBase.TagIsoEquivalent, out var i)) iso = i;
                if (sub.TryGetDouble(ExifDirectoryBase.TagFocalLength, out var f)) focal = f;
                if (sub.TryGetInt32(ExifDirectoryBase.TagExifImageWidth, out var w)) width = w;
                if (sub.TryGetInt32(ExifDirectoryBase.TagExifImageHeight, out var h)) height = h;
                if (dateTaken == null && sub.TryGetDateTime(ExifDirectoryBase.TagDateTimeOriginal, out var dto)) dateTaken = dto;
            }
            if (dir is GpsDirectory gps)
            {
                var geo = gps.GetGeoLocation();
                if (geo != null) { lat = geo.Value.Latitude; lon = geo.Value.Longitude; }
                if (gps.TryGetDouble(GpsDirectory.TagAltitude, out var a)) alt = a;
            }
        }

        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = @"UPDATE media SET has_exif=1, date_taken=@dt, camera_model=@cam, lens_model=@lens,
            iso=@iso, aperture=@ap, shutter_speed=@ss, focal_length=@fl, width=COALESCE(@w,width), height=COALESCE(@h,height),
            orientation=@ori, latitude=@lat, longitude=@lon, altitude=@alt WHERE id=@id";
        cmd.Parameters.AddWithValue("@dt", dateTaken.HasValue ? dateTaken.Value.ToString("o") : (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@cam", (object?)camera ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@lens", (object?)lens ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@iso", iso.HasValue ? iso.Value : (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@ap", (object?)aperture ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ss", (object?)shutter ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@fl", focal.HasValue ? focal.Value : (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@w", width.HasValue ? width.Value : (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@h", height.HasValue ? height.Value : (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@ori", orientation.HasValue ? orientation.Value : (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@lat", lat.HasValue ? lat.Value : (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@lon", lon.HasValue ? lon.Value : (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@alt", alt.HasValue ? alt.Value : (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@id", item.Id);
        cmd.ExecuteNonQuery();
    }

    private List<MediaItem> GetItemsNeedingExif()
    {
        var items = new List<MediaItem>();
        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = "SELECT id, file_path, file_name FROM media WHERE has_exif=0 AND media_type NOT IN ('Video','SlowMotion') ORDER BY id";
        using var r = cmd.ExecuteReader();
        while (r.Read()) items.Add(new MediaItem { Id = r.GetInt64(0), FilePath = r.GetString(1), FileName = r.GetString(2) });
        return items;
    }
}
