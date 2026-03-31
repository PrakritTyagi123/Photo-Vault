using PhotoVault.Core.Data;
using PhotoVault.Core.Models;
using PhotoVault.Core.Interfaces;

namespace PhotoVault.Services;

public class FileDiscoveryService : IFileDiscoveryService
{
    private readonly DatabaseService _db;
    private readonly MediaRepository _repo;

    private static readonly HashSet<string> PhotoExts = new(StringComparer.OrdinalIgnoreCase)
    { ".jpg",".jpeg",".png",".bmp",".tiff",".tif",".webp",".heic",".heif",".avif" };
    private static readonly HashSet<string> VideoExts = new(StringComparer.OrdinalIgnoreCase)
    { ".mp4",".mov",".avi",".mkv",".wmv",".flv",".m4v",".3gp",".webm",".mts" };
    private static readonly HashSet<string> RawExts = new(StringComparer.OrdinalIgnoreCase)
    { ".cr2",".cr3",".nef",".arw",".dng",".orf",".rw2",".raf",".srw",".pef" };
    private static readonly HashSet<string> GifExts = new(StringComparer.OrdinalIgnoreCase) { ".gif" };

    public Dictionary<string, List<long>> SubfolderMap { get; } = new();

    public FileDiscoveryService(DatabaseService db, MediaRepository repo)
    {
        _db = db; _repo = repo;
    }

    public Task<int> ScanFolderAsync(string folderPath, IProgress<int>? progress = null)
    {
        SubfolderMap.Clear();
        if (!Directory.Exists(folderPath)) return Task.FromResult(0);

        var files = Directory.EnumerateFiles(folderPath, "*.*", SearchOption.AllDirectories)
            .Where(f => IsSupported(Path.GetExtension(f))).ToList();

        int added = 0;
        foreach (var file in files)
        {
            var fi = new FileInfo(file);
            var ext = fi.Extension.ToLowerInvariant();
            var mediaType = ClassifyType(ext, fi.Name);

            var item = new MediaItem
            {
                FilePath = fi.FullName, FileName = fi.Name, FileExtension = ext,
                FileSize = fi.Length, MediaType = mediaType, DateModified = fi.LastWriteTime,
            };

            var id = _repo.Insert(item);
            if (id > 0)
            {
                added++;
                progress?.Report(added);

                // Track subfolder
                var parentDir = fi.DirectoryName ?? "";
                if (!string.Equals(parentDir, folderPath, StringComparison.OrdinalIgnoreCase))
                {
                    var folderName = Path.GetFileName(parentDir);
                    if (!string.IsNullOrEmpty(folderName))
                    {
                        if (!SubfolderMap.ContainsKey(folderName)) SubfolderMap[folderName] = new List<long>();
                        SubfolderMap[folderName].Add(id);
                    }
                }
            }
        }
        return Task.FromResult(added);
    }

    private static bool IsSupported(string ext) => PhotoExts.Contains(ext) || VideoExts.Contains(ext) || RawExts.Contains(ext) || GifExts.Contains(ext);

    private static MediaType ClassifyType(string ext, string fileName)
    {
        if (GifExts.Contains(ext)) return MediaType.Gif;
        if (RawExts.Contains(ext)) return MediaType.Raw;
        if (VideoExts.Contains(ext))
        {
            if (fileName.Contains("SLOMO", StringComparison.OrdinalIgnoreCase) || fileName.Contains("slow", StringComparison.OrdinalIgnoreCase))
                return MediaType.SlowMotion;
            return MediaType.Video;
        }
        if (fileName.Contains("screenshot", StringComparison.OrdinalIgnoreCase) || fileName.Contains("Screen Shot", StringComparison.OrdinalIgnoreCase))
            return MediaType.Screenshot;
        if (fileName.Contains("pano", StringComparison.OrdinalIgnoreCase)) return MediaType.Panorama;
        return MediaType.Photo;
    }
}
