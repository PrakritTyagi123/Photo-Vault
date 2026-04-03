namespace PhotoVault.Core.Models;

public class Album
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public AlbumType Type { get; set; } = AlbumType.Manual;
    public long? CoverMediaId { get; set; }
    public string? SmartQuery { get; set; }
    public DateTime DateCreated { get; set; } = DateTime.UtcNow;
}

public enum AlbumType { Manual, Auto }
