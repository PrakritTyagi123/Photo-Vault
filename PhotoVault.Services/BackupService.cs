using System.IO.Compression;
using PhotoVault.Core.Data;

namespace PhotoVault.Services;

public class BackupService
{
    private readonly DatabaseService _db; private readonly LogService _log; private readonly string _dbPath; private readonly string _thumbDir;
    public BackupService(DatabaseService db, LogService log, string dbPath, string thumbDir) { _db = db; _log = log; _dbPath = dbPath; _thumbDir = thumbDir; }

    public async Task<string?> CreateBackupAsync(string outDir, bool includeThumbs = true, IProgress<string>? progress = null)
    {
        try
        {
            Directory.CreateDirectory(outDir);
            var path = Path.Combine(outDir, $"PhotoVault_Backup_{DateTime.Now:yyyyMMdd_HHmmss}.zip");
            progress?.Report("Creating backup...");
            using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
            if (File.Exists(_dbPath)) { var tmp = _dbPath + ".bak"; File.Copy(_dbPath, tmp, true); zip.CreateEntryFromFile(tmp, "photovault.db", CompressionLevel.Fastest); File.Delete(tmp); }
            if (includeThumbs && Directory.Exists(_thumbDir))
            {
                var files = Directory.EnumerateFiles(_thumbDir, "*.*", SearchOption.AllDirectories).ToList();
                int done = 0;
                foreach (var f in files) { zip.CreateEntryFromFile(f, Path.Combine("thumbnails", Path.GetRelativePath(_thumbDir, f)), CompressionLevel.NoCompression); done++; if (done % 100 == 0) progress?.Report($"Thumbnails: {done}/{files.Count}"); }
            }
            _log.Info("Backup", $"Created: {path}"); progress?.Report("Backup complete"); return path;
        }
        catch (Exception ex) { _log.Error("Backup", ex.Message); progress?.Report($"Error: {ex.Message}"); return null; }
    }

    public List<BackupInfo> GetExistingBackups(string dir)
    {
        if (!Directory.Exists(dir)) return new();
        return Directory.GetFiles(dir, "PhotoVault_Backup_*.zip").Select(f => new FileInfo(f)).Select(fi => new BackupInfo { Path = fi.FullName, FileName = fi.Name, Size = fi.Length, Created = fi.CreationTime }).OrderByDescending(b => b.Created).ToList();
    }
}

public class BackupInfo
{
    public string Path { get; set; } = ""; public string FileName { get; set; } = ""; public long Size { get; set; } public DateTime Created { get; set; }
    public string SizeDisplay => Size > 1024L * 1024 * 1024 ? $"{Size / (1024.0 * 1024 * 1024):N1} GB" : $"{Size / (1024.0 * 1024):N0} MB";
    public string DateDisplay => Created.ToString("dd MMM yyyy, HH:mm");
}
