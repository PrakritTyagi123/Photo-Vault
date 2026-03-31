using System.IO.Compression;
using PhotoVault.Core.Data;
using PhotoVault.Core.Models;

namespace PhotoVault.Services;

public class ExportService
{
    private readonly DatabaseService _db; private readonly AlbumRepository _albumRepo; private readonly LogService _log;
    public ExportService(DatabaseService db, AlbumRepository albumRepo, LogService log) { _db = db; _albumRepo = albumRepo; _log = log; }

    public async Task<int> ExportAlbumToFolderAsync(long albumId, string outPath, IProgress<(int done, int total)>? progress = null)
    {
        var items = _albumRepo.GetAlbumMedia(albumId); Directory.CreateDirectory(outPath); int done = 0;
        foreach (var item in items)
        {
            if (File.Exists(item.FilePath)) { var dest = Path.Combine(outPath, item.FileName); int n = 1; while (File.Exists(dest)) { dest = Path.Combine(outPath, $"{Path.GetFileNameWithoutExtension(item.FileName)}_{n}{item.FileExtension}"); n++; } await Task.Run(() => File.Copy(item.FilePath, dest)); }
            done++; progress?.Report((done, items.Count));
        }
        _log.Info("Export", $"Exported {done} files"); return done;
    }

    public async Task<string?> ExportAlbumAsZipAsync(long albumId, string outDir, IProgress<(int done, int total)>? progress = null)
    {
        var album = _albumRepo.GetAllAlbums().FirstOrDefault(a => a.Id == albumId); if (album == null) return null;
        var items = _albumRepo.GetAlbumMedia(albumId);
        var zipPath = Path.Combine(outDir, $"{album.Name.Replace(" ", "_")}_{DateTime.Now:yyyyMMdd}.zip");
        using var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create);
        int done = 0;
        foreach (var item in items) { if (File.Exists(item.FilePath)) zip.CreateEntryFromFile(item.FilePath, item.FileName, CompressionLevel.Fastest); done++; progress?.Report((done, items.Count)); }
        _log.Info("Export", $"Zip: {zipPath}"); return zipPath;
    }
}
