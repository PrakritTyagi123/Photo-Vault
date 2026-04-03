namespace PhotoVault.Core.Interfaces;

public interface IFileDiscoveryService
{
    Task<int> ScanFolderAsync(string folderPath, IProgress<int>? progress = null);
    Dictionary<string, List<long>> SubfolderMap { get; }
}
