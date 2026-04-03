using PhotoVault.Core.Models;
using Microsoft.Data.Sqlite;

namespace PhotoVault.Core.Data;

public class AlbumRepository
{
    private readonly DatabaseService _db;
    public AlbumRepository(DatabaseService db) { _db = db; }

    public long CreateAlbum(string name, AlbumType type, string? smartQuery = null)
    {
        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = "INSERT INTO albums (name, type, smart_query, date_created) VALUES (@n, @t, @q, @d); SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("@n", name); cmd.Parameters.AddWithValue("@t", type.ToString());
        cmd.Parameters.AddWithValue("@q", (object?)smartQuery ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@d", DateTime.UtcNow.ToString("o"));
        return Convert.ToInt64(cmd.ExecuteScalar());
    }

    public void DeleteAlbum(long id)
    {
        using var c1 = _db.Connection.CreateCommand(); c1.CommandText = "DELETE FROM album_media WHERE album_id=@id"; c1.Parameters.AddWithValue("@id", id); c1.ExecuteNonQuery();
        using var c2 = _db.Connection.CreateCommand(); c2.CommandText = "DELETE FROM albums WHERE id=@id"; c2.Parameters.AddWithValue("@id", id); c2.ExecuteNonQuery();
    }

    public void SetCover(long albumId, long mediaId)
    {
        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = "UPDATE albums SET cover_media_id=@m WHERE id=@a";
        cmd.Parameters.AddWithValue("@m", mediaId); cmd.Parameters.AddWithValue("@a", albumId); cmd.ExecuteNonQuery();
    }

    public void AutoSetCover(long albumId)
    {
        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = @"UPDATE albums SET cover_media_id=(
            SELECT m.id FROM media m INNER JOIN album_media am ON m.id=am.media_id
            WHERE am.album_id=@a AND m.has_thumbnail=1
            ORDER BY COALESCE(m.date_taken,m.date_imported) DESC LIMIT 1) WHERE id=@a";
        cmd.Parameters.AddWithValue("@a", albumId); cmd.ExecuteNonQuery();
    }

    public void AutoSetAllCovers()
    {
        foreach (var a in GetAllAlbums().Where(a => a.Type == AlbumType.Manual)) AutoSetCover(a.Id);
    }

    public void AddMedia(long albumId, long mediaId)
    {
        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = @"INSERT OR IGNORE INTO album_media (album_id,media_id,sort_order,date_added)
            VALUES(@a,@m,(SELECT COALESCE(MAX(sort_order),0)+1 FROM album_media WHERE album_id=@a),@d)";
        cmd.Parameters.AddWithValue("@a", albumId); cmd.Parameters.AddWithValue("@m", mediaId);
        cmd.Parameters.AddWithValue("@d", DateTime.UtcNow.ToString("o")); cmd.ExecuteNonQuery();
    }

    public void AddMediaBatch(long albumId, IEnumerable<long> ids) { foreach (var id in ids) AddMedia(albumId, id); }

    public void RemoveMedia(long albumId, long mediaId)
    {
        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = "DELETE FROM album_media WHERE album_id=@a AND media_id=@m";
        cmd.Parameters.AddWithValue("@a", albumId); cmd.Parameters.AddWithValue("@m", mediaId); cmd.ExecuteNonQuery();
    }

    public List<Album> GetAllAlbums()
    {
        var list = new List<Album>();
        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM albums ORDER BY type, name";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            Enum.TryParse<AlbumType>(r.IsDBNull(r.GetOrdinal("type")) ? "Manual" : r.GetString(r.GetOrdinal("type")), out var at);
            list.Add(new Album
            {
                Id = r.GetInt64(r.GetOrdinal("id")), Name = r.GetString(r.GetOrdinal("name")), Type = at,
                CoverMediaId = r.IsDBNull(r.GetOrdinal("cover_media_id")) ? null : r.GetInt64(r.GetOrdinal("cover_media_id")),
                SmartQuery = r.IsDBNull(r.GetOrdinal("smart_query")) ? null : r.GetString(r.GetOrdinal("smart_query")),
            });
        }
        return list;
    }

    public int GetAlbumMediaCount(long id)
    {
        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(1) FROM album_media WHERE album_id=@id"; cmd.Parameters.AddWithValue("@id", id);
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    public List<MediaItem> GetAlbumMedia(long albumId)
    {
        var items = new List<MediaItem>();
        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = "SELECT m.* FROM media m INNER JOIN album_media am ON m.id=am.media_id WHERE am.album_id=@id ORDER BY COALESCE(m.date_taken,m.date_imported) DESC";
        cmd.Parameters.AddWithValue("@id", albumId);
        using var r = cmd.ExecuteReader();
        while (r.Read()) items.Add(ReadBasic(r));
        return items;
    }

    public string? GetCoverThumbnail(long albumId)
    {
        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = "SELECT m.thumbnail_small FROM media m INNER JOIN albums a ON m.id=a.cover_media_id WHERE a.id=@id";
        cmd.Parameters.AddWithValue("@id", albumId);
        var r = cmd.ExecuteScalar(); if (r != null && r != DBNull.Value) return r.ToString();

        using var cmd2 = _db.Connection.CreateCommand();
        cmd2.CommandText = "SELECT m.thumbnail_small FROM media m INNER JOIN album_media am ON m.id=am.media_id WHERE am.album_id=@id AND m.has_thumbnail=1 ORDER BY COALESCE(m.date_taken,m.date_imported) DESC LIMIT 1";
        cmd2.Parameters.AddWithValue("@id", albumId);
        var r2 = cmd2.ExecuteScalar(); return r2 != null && r2 != DBNull.Value ? r2.ToString() : null;
    }

    public string? GetSmartAlbumCover(string where)
    {
        try { using var cmd = _db.Connection.CreateCommand(); cmd.CommandText = $"SELECT thumbnail_small FROM media WHERE in_vault=0 AND has_thumbnail=1 AND ({where}) ORDER BY COALESCE(date_taken,date_imported) DESC LIMIT 1"; var r = cmd.ExecuteScalar(); return r != null && r != DBNull.Value ? r.ToString() : null; } catch { return null; }
    }

    public int GetSmartAlbumCount(string where)
    {
        try { using var cmd = _db.Connection.CreateCommand(); cmd.CommandText = $"SELECT COUNT(1) FROM media WHERE in_vault=0 AND ({where})"; return Convert.ToInt32(cmd.ExecuteScalar()); } catch { return 0; }
    }

    public List<MediaItem> GetSmartAlbumMedia(string where)
    {
        var items = new List<MediaItem>();
        try { using var cmd = _db.Connection.CreateCommand(); cmd.CommandText = $"SELECT * FROM media WHERE in_vault=0 AND ({where}) ORDER BY COALESCE(date_taken,date_imported) DESC LIMIT 200"; using var r = cmd.ExecuteReader(); while (r.Read()) items.Add(ReadBasic(r)); } catch { }
        return items;
    }

    public void EnsureDefaultAlbums()
    {
        var existing = GetAllAlbums();
        var defaults = new[] { ("Favorites", "is_favorite=1"), ("Videos", "media_type='Video'"), ("Screenshots", "media_type='Screenshot'"), ("RAW Files", "media_type='Raw'"), ("GIFs", "media_type='Gif'") };
        foreach (var (name, query) in defaults)
            if (!existing.Any(a => a.Name == name && a.Type == AlbumType.Auto)) CreateAlbum(name, AlbumType.Auto, query);
    }

    private static MediaItem ReadBasic(SqliteDataReader r)
    {
        Enum.TryParse<MediaType>(r.IsDBNull(r.GetOrdinal("media_type")) ? "Photo" : r.GetString(r.GetOrdinal("media_type")), out var mt);
        return new MediaItem
        {
            Id = r.GetInt64(r.GetOrdinal("id")), FilePath = r.GetString(r.GetOrdinal("file_path")),
            FileName = r.GetString(r.GetOrdinal("file_name")), FileExtension = r.GetString(r.GetOrdinal("file_extension")),
            FileSize = r.GetInt64(r.GetOrdinal("file_size")), MediaType = mt,
            DateTaken = r.IsDBNull(r.GetOrdinal("date_taken")) ? null : (DateTime.TryParse(r.GetString(r.GetOrdinal("date_taken")), out var dt) ? dt : null),
            DateImported = DateTime.TryParse(r.GetString(r.GetOrdinal("date_imported")), out var di) ? di : DateTime.UtcNow,
            HasThumbnail = !r.IsDBNull(r.GetOrdinal("has_thumbnail")) && r.GetInt32(r.GetOrdinal("has_thumbnail")) == 1,
            ThumbnailSmall = r.IsDBNull(r.GetOrdinal("thumbnail_small")) ? null : r.GetString(r.GetOrdinal("thumbnail_small")),
            ThumbnailMedium = r.IsDBNull(r.GetOrdinal("thumbnail_medium")) ? null : r.GetString(r.GetOrdinal("thumbnail_medium")),
            ThumbnailLarge = r.IsDBNull(r.GetOrdinal("thumbnail_large")) ? null : r.GetString(r.GetOrdinal("thumbnail_large")),
            StarRating = r.IsDBNull(r.GetOrdinal("star_rating")) ? 0 : r.GetInt32(r.GetOrdinal("star_rating")),
            IsFavorite = !r.IsDBNull(r.GetOrdinal("is_favorite")) && r.GetInt32(r.GetOrdinal("is_favorite")) == 1,
        };
    }
}
