namespace PhotoVault.Core.Models;

public class MediaItem
{
    public long Id { get; set; }
    public string FilePath { get; set; } = "";
    public string FileName { get; set; } = "";
    public string FileExtension { get; set; } = "";
    public long FileSize { get; set; }
    public string FileHash { get; set; } = "";
    public MediaType MediaType { get; set; } = MediaType.Photo;

    public DateTime? DateTaken { get; set; }
    public DateTime DateImported { get; set; } = DateTime.UtcNow;
    public DateTime DateModified { get; set; }

    public string? CameraModel { get; set; }
    public string? LensModel { get; set; }
    public int? Iso { get; set; }
    public string? Aperture { get; set; }
    public string? ShutterSpeed { get; set; }
    public double? FocalLength { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    public int? Orientation { get; set; }

    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public double? Altitude { get; set; }

    public string? Country { get; set; }
    public string? City { get; set; }
    public string? Address { get; set; }

    public string? Caption { get; set; }
    public string? Tags { get; set; }
    public string? Vibe { get; set; }
    public double? QualityScore { get; set; }
    public string? OcrText { get; set; }
    public bool IsNsfw { get; set; }

    public int StarRating { get; set; }
    public bool IsFavorite { get; set; }
    public bool InVault { get; set; }

    public bool HasThumbnail { get; set; }
    public bool HasExif { get; set; }
    public bool HasFaces { get; set; }
    public bool HasTags { get; set; }
    public bool HasClipEmbedding { get; set; }
    public bool HasCaption { get; set; }

    public string? ThumbnailSmall { get; set; }
    public string? ThumbnailMedium { get; set; }
    public string? ThumbnailLarge { get; set; }

    public DateTime DisplayDate => DateTaken ?? DateImported;
    public string MonthGroup => DisplayDate.ToString("MMMM yyyy");
    public string FileSizeDisplay
    {
        get
        {
            if (FileSize < 1024) return $"{FileSize} B";
            if (FileSize < 1024 * 1024) return $"{FileSize / 1024.0:N0} KB";
            if (FileSize < 1024L * 1024 * 1024) return $"{FileSize / (1024.0 * 1024):N1} MB";
            return $"{FileSize / (1024.0 * 1024 * 1024):N2} GB";
        }
    }
}

public enum MediaType
{
    Photo, Video, Gif, Screenshot, Selfie, Panorama, Raw, SlowMotion
}
