using PhotoVault.Core.Data;
using PhotoVault.Core.Models;

namespace PhotoVault.Services;

public class SearchService
{
    private readonly DatabaseService _db;
    public SearchService(DatabaseService db) { _db = db; }

    public void RebuildIndex()
    {
        using var del = _db.Connection.CreateCommand();
        del.CommandText = "DELETE FROM media_fts"; del.ExecuteNonQuery();

        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = @"INSERT INTO media_fts(rowid, file_name, caption, tags, city, country, camera_model, vibe, ocr_text)
            SELECT id, file_name, COALESCE(caption,''), COALESCE(tags,''), COALESCE(city,''), COALESCE(country,''),
            COALESCE(camera_model,''), COALESCE(vibe,''), COALESCE(ocr_text,'') FROM media WHERE in_vault=0";
        cmd.ExecuteNonQuery();
    }

    public List<MediaItem> Search(SearchQuery q)
    {
        var items = new List<MediaItem>();
        var conditions = new List<string> { "m.in_vault=0" };
        var parameters = new Dictionary<string, object>();

        if (!string.IsNullOrWhiteSpace(q.Text))
        {
            conditions.Add("m.id IN (SELECT rowid FROM media_fts WHERE media_fts MATCH @text)");
            parameters["@text"] = q.Text.Trim().Replace("'", "") + "*";
        }
        if (!string.IsNullOrEmpty(q.Month))
        {
            var monthNum = Array.IndexOf(new[] { "", "January","February","March","April","May","June","July","August","September","October","November","December" }, q.Month);
            if (monthNum > 0) { conditions.Add($"CAST(strftime('%m', COALESCE(m.date_taken,m.date_imported)) AS INTEGER)=@month"); parameters["@month"] = monthNum; }
        }
        if (!string.IsNullOrEmpty(q.Location)) { conditions.Add("(m.city LIKE @loc OR m.country LIKE @loc)"); parameters["@loc"] = $"%{q.Location}%"; }
        if (!string.IsNullOrEmpty(q.Camera)) { conditions.Add("m.camera_model LIKE @cam"); parameters["@cam"] = $"%{q.Camera}%"; }
        if (!string.IsNullOrEmpty(q.MediaType)) { conditions.Add("m.media_type=@mt"); parameters["@mt"] = q.MediaType; }
        if (!string.IsNullOrEmpty(q.Lens)) { conditions.Add("m.lens_model LIKE @lens"); parameters["@lens"] = $"%{q.Lens}%"; }
        if (!string.IsNullOrEmpty(q.Vibe)) { conditions.Add("m.vibe=@vibe"); parameters["@vibe"] = q.Vibe; }

        using var cmd = _db.Connection.CreateCommand();
        cmd.CommandText = $"SELECT m.* FROM media m WHERE {string.Join(" AND ", conditions)} ORDER BY COALESCE(m.date_taken,m.date_imported) DESC LIMIT 200";
        foreach (var p in parameters) cmd.Parameters.AddWithValue(p.Key, p.Value);

        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            Enum.TryParse<MediaType>(r.IsDBNull(r.GetOrdinal("media_type")) ? "Photo" : r.GetString(r.GetOrdinal("media_type")), out var mt);
            items.Add(new MediaItem
            {
                Id = r.GetInt64(r.GetOrdinal("id")), FilePath = r.GetString(r.GetOrdinal("file_path")),
                FileName = r.GetString(r.GetOrdinal("file_name")), FileExtension = r.GetString(r.GetOrdinal("file_extension")),
                FileSize = r.GetInt64(r.GetOrdinal("file_size")), MediaType = mt,
                HasThumbnail = !r.IsDBNull(r.GetOrdinal("has_thumbnail")) && r.GetInt32(r.GetOrdinal("has_thumbnail")) == 1,
                ThumbnailSmall = r.IsDBNull(r.GetOrdinal("thumbnail_small")) ? null : r.GetString(r.GetOrdinal("thumbnail_small")),
            });
        }
        return items;
    }

    public List<string> GetDistinctValues(string field)
    {
        var values = new List<string>();
        string col = field switch
        {
            "camera" => "camera_model", "location" => "city", "type" => "media_type",
            "lens" => "lens_model", "vibe" => "vibe", _ => field
        };
        try
        {
            using var cmd = _db.Connection.CreateCommand();
            cmd.CommandText = $"SELECT DISTINCT {col} FROM media WHERE {col} IS NOT NULL AND {col}!='' AND in_vault=0 ORDER BY {col} LIMIT 50";
            using var r = cmd.ExecuteReader();
            while (r.Read()) values.Add(r.GetString(0));
        }
        catch { }
        return values;
    }
}

public class SearchQuery
{
    public string Text { get; set; } = "";
    public string Month { get; set; } = "";
    public string Location { get; set; } = "";
    public string Camera { get; set; } = "";
    public string Person { get; set; } = "";
    public string MediaType { get; set; } = "";
    public string Lens { get; set; } = "";
    public string Vibe { get; set; } = "";
    public bool HasAnyFilter => !string.IsNullOrWhiteSpace(Text) || !string.IsNullOrEmpty(Month) || !string.IsNullOrEmpty(Location) || !string.IsNullOrEmpty(Camera) || !string.IsNullOrEmpty(MediaType) || !string.IsNullOrEmpty(Lens) || !string.IsNullOrEmpty(Vibe);
}
