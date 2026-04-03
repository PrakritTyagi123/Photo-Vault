using Microsoft.Data.Sqlite;

namespace PhotoVault.Core.Data;

public class DatabaseService
{
    public SqliteConnection Connection { get; }

    public DatabaseService(string dbPath)
    {
        Connection = new SqliteConnection($"Data Source={dbPath}");
        Connection.Open();
    }

    public void Initialize()
    {
        using var pragma = Connection.CreateCommand();
        pragma.CommandText = "PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL; PRAGMA foreign_keys=ON;";
        pragma.ExecuteNonQuery();

        using var cmd = Connection.CreateCommand();
        cmd.CommandText = @"
        CREATE TABLE IF NOT EXISTS media (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            file_path TEXT UNIQUE NOT NULL,
            file_name TEXT NOT NULL,
            file_extension TEXT NOT NULL,
            file_size INTEGER NOT NULL,
            file_hash TEXT,
            media_type TEXT DEFAULT 'Photo',
            date_taken TEXT,
            date_imported TEXT NOT NULL,
            date_modified TEXT,
            camera_model TEXT, lens_model TEXT,
            iso INTEGER, aperture TEXT, shutter_speed TEXT, focal_length REAL,
            width INTEGER, height INTEGER, orientation INTEGER,
            latitude REAL, longitude REAL, altitude REAL,
            country TEXT, city TEXT, address TEXT,
            caption TEXT, tags TEXT, vibe TEXT,
            quality_score REAL, ocr_text TEXT, is_nsfw INTEGER DEFAULT 0,
            star_rating INTEGER DEFAULT 0, is_favorite INTEGER DEFAULT 0, in_vault INTEGER DEFAULT 0,
            has_thumbnail INTEGER DEFAULT 0, has_exif INTEGER DEFAULT 0,
            has_faces INTEGER DEFAULT 0, has_tags INTEGER DEFAULT 0,
            has_clip_embedding INTEGER DEFAULT 0, has_caption INTEGER DEFAULT 0,
            thumbnail_small TEXT, thumbnail_medium TEXT, thumbnail_large TEXT
        );

        CREATE TABLE IF NOT EXISTS persons (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            name TEXT, face_count INTEGER DEFAULT 0, thumbnail_path TEXT
        );

        CREATE TABLE IF NOT EXISTS faces (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            media_id INTEGER REFERENCES media(id),
            person_id INTEGER REFERENCES persons(id),
            x REAL, y REAL, w REAL, h REAL,
            embedding BLOB, confidence REAL
        );

        CREATE TABLE IF NOT EXISTS albums (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            name TEXT NOT NULL, type TEXT DEFAULT 'Manual',
            cover_media_id INTEGER, smart_query TEXT, date_created TEXT
        );

        CREATE TABLE IF NOT EXISTS album_media (
            album_id INTEGER REFERENCES albums(id),
            media_id INTEGER REFERENCES media(id),
            sort_order INTEGER DEFAULT 0, date_added TEXT,
            PRIMARY KEY (album_id, media_id)
        );

        CREATE TABLE IF NOT EXISTS trips (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            name TEXT NOT NULL, start_date TEXT, end_date TEXT,
            country TEXT, city TEXT, photo_count INTEGER DEFAULT 0
        );

        CREATE TABLE IF NOT EXISTS trip_media (
            trip_id INTEGER REFERENCES trips(id),
            media_id INTEGER REFERENCES media(id),
            PRIMARY KEY (trip_id, media_id)
        );

        CREATE TABLE IF NOT EXISTS clip_embeddings (
            media_id INTEGER PRIMARY KEY REFERENCES media(id),
            embedding BLOB NOT NULL
        );

        CREATE TABLE IF NOT EXISTS scan_records (
            step_id TEXT PRIMARY KEY,
            status TEXT, progress INTEGER DEFAULT 0, last_run TEXT
        );

        CREATE TABLE IF NOT EXISTS watched_folders (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            path TEXT UNIQUE NOT NULL, is_active INTEGER DEFAULT 1,
            date_added TEXT, file_count INTEGER DEFAULT 0, total_size INTEGER DEFAULT 0
        );

        CREATE TABLE IF NOT EXISTS settings (
            key TEXT PRIMARY KEY, value TEXT
        );

        CREATE VIRTUAL TABLE IF NOT EXISTS media_fts USING fts5(
            file_name, caption, tags, city, country, camera_model, vibe, ocr_text,
            content='media',
            content_rowid='id'
        );";
        cmd.ExecuteNonQuery();
    }
}
