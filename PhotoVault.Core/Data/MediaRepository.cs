using PhotoVault.Core.Models;
using Microsoft.Data.Sqlite;

namespace PhotoVault.Core.Data;

public class MediaRepository
{
    private readonly DatabaseService _db;
    public MediaRepository(DatabaseService db) { _db = db; }

    public int GetCount()
    {
        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM media WHERE in_vault = 0";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    public List<MediaItem> GetAll(int limit = 5000)
    {
        var items = new List<MediaItem>();
        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM media WHERE in_vault = 0 ORDER BY COALESCE(date_taken, date_imported) DESC LIMIT @limit";
        cmd.Parameters.AddWithValue("@limit", limit);
        using var r = cmd.ExecuteReader();
        while (r.Read()) items.Add(ReadFull(r));
        return items;
    }

    public MediaItem? GetById(long id)
    {
        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM media WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        using var r = cmd.ExecuteReader();
        return r.Read() ? ReadFull(r) : null;
    }

    public long Insert(MediaItem item)
    {
        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = @"INSERT OR IGNORE INTO media (file_path, file_name, file_extension, file_size, media_type, date_imported, date_modified)
                            VALUES (@fp, @fn, @fe, @fs, @mt, @di, @dm); SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("@fp", item.FilePath);
        cmd.Parameters.AddWithValue("@fn", item.FileName);
        cmd.Parameters.AddWithValue("@fe", item.FileExtension);
        cmd.Parameters.AddWithValue("@fs", item.FileSize);
        cmd.Parameters.AddWithValue("@mt", item.MediaType.ToString());
        cmd.Parameters.AddWithValue("@di", DateTime.UtcNow.ToString("o"));
        cmd.Parameters.AddWithValue("@dm", item.DateModified.ToString("o"));
        var result = cmd.ExecuteScalar();
        return result != null ? Convert.ToInt64(result) : 0;
    }

    public void UpdateRating(long mediaId, int rating)
    {
        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = "UPDATE media SET star_rating = @r WHERE id = @id";
        cmd.Parameters.AddWithValue("@r", rating);
        cmd.Parameters.AddWithValue("@id", mediaId);
        cmd.ExecuteNonQuery();
    }

    public void UpdateFavorite(long mediaId, bool isFavorite)
    {
        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = "UPDATE media SET is_favorite = @f WHERE id = @id";
        cmd.Parameters.AddWithValue("@f", isFavorite ? 1 : 0);
        cmd.Parameters.AddWithValue("@id", mediaId);
        cmd.ExecuteNonQuery();
    }

    public void Delete(long id)
    {
        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = "DELETE FROM album_media WHERE media_id = @id"; cmd.Parameters.AddWithValue("@id", id); cmd.ExecuteNonQuery();
        using var cmd2 = _db.Connection.CreateCommand();
        cmd2.CommandText = "DELETE FROM media WHERE id = @id"; cmd2.Parameters.AddWithValue("@id", id); cmd2.ExecuteNonQuery();
    }

    private static MediaItem ReadFull(SqliteDataReader r)
    {
        var item = new MediaItem();
        item.Id = r.GetInt64(r.GetOrdinal("id"));
        item.FilePath = r.GetString(r.GetOrdinal("file_path"));
        item.FileName = r.GetString(r.GetOrdinal("file_name"));
        item.FileExtension = r.GetString(r.GetOrdinal("file_extension"));
        item.FileSize = r.GetInt64(r.GetOrdinal("file_size"));
        item.FileHash = GetStringSafe(r, "file_hash");
        Enum.TryParse<MediaType>(GetStringSafe(r, "media_type") ?? "Photo", out var mt); item.MediaType = mt;
        item.DateTaken = GetDateSafe(r, "date_taken");
        item.DateImported = GetDateSafe(r, "date_imported") ?? DateTime.UtcNow;
        item.DateModified = GetDateSafe(r, "date_modified") ?? DateTime.UtcNow;
        item.CameraModel = GetStringSafe(r, "camera_model");
        item.LensModel = GetStringSafe(r, "lens_model");
        item.Iso = GetIntSafe(r, "iso");
        item.Aperture = GetStringSafe(r, "aperture");
        item.ShutterSpeed = GetStringSafe(r, "shutter_speed");
        item.FocalLength = GetDoubleSafe(r, "focal_length");
        item.Width = GetIntSafe(r, "width");
        item.Height = GetIntSafe(r, "height");
        item.Orientation = GetIntSafe(r, "orientation");
        item.Latitude = GetDoubleSafe(r, "latitude");
        item.Longitude = GetDoubleSafe(r, "longitude");
        item.Altitude = GetDoubleSafe(r, "altitude");
        item.Country = GetStringSafe(r, "country");
        item.City = GetStringSafe(r, "city");
        item.Address = GetStringSafe(r, "address");
        item.Caption = GetStringSafe(r, "caption");
        item.Tags = GetStringSafe(r, "tags");
        item.Vibe = GetStringSafe(r, "vibe");
        item.QualityScore = GetDoubleSafe(r, "quality_score");
        item.OcrText = GetStringSafe(r, "ocr_text");
        item.IsNsfw = GetIntSafe(r, "is_nsfw") == 1;
        item.StarRating = GetIntSafe(r, "star_rating") ?? 0;
        item.IsFavorite = GetIntSafe(r, "is_favorite") == 1;
        item.InVault = GetIntSafe(r, "in_vault") == 1;
        item.HasThumbnail = GetIntSafe(r, "has_thumbnail") == 1;
        item.HasExif = GetIntSafe(r, "has_exif") == 1;
        item.HasFaces = GetIntSafe(r, "has_faces") == 1;
        item.HasTags = GetIntSafe(r, "has_tags") == 1;
        item.HasClipEmbedding = GetIntSafe(r, "has_clip_embedding") == 1;
        item.HasCaption = GetIntSafe(r, "has_caption") == 1;
        item.ThumbnailSmall = GetStringSafe(r, "thumbnail_small");
        item.ThumbnailMedium = GetStringSafe(r, "thumbnail_medium");
        item.ThumbnailLarge = GetStringSafe(r, "thumbnail_large");
        return item;
    }

    private static string? GetStringSafe(SqliteDataReader r, string col)
    {
        var ord = r.GetOrdinal(col); return r.IsDBNull(ord) ? null : r.GetString(ord);
    }
    private static int? GetIntSafe(SqliteDataReader r, string col)
    {
        var ord = r.GetOrdinal(col); return r.IsDBNull(ord) ? null : r.GetInt32(ord);
    }
    private static double? GetDoubleSafe(SqliteDataReader r, string col)
    {
        var ord = r.GetOrdinal(col); return r.IsDBNull(ord) ? null : r.GetDouble(ord);
    }
    private static DateTime? GetDateSafe(SqliteDataReader r, string col)
    {
        var ord = r.GetOrdinal(col);
        if (r.IsDBNull(ord)) return null;
        return DateTime.TryParse(r.GetString(ord), out var d) ? d : null;
    }
}
