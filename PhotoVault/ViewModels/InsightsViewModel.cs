using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PhotoVault.Services;

namespace PhotoVault.ViewModels;

public partial class InsightsViewModel : ObservableObject
{
    private readonly InsightsService _insights;
    [ObservableProperty] private int _gpsCount, _favoriteCount;
    [ObservableProperty] private double _avgQuality;
    [ObservableProperty] private ObservableCollection<NameCount> _topCameras = new(), _topLocations = new(), _topFormats = new();
    [ObservableProperty] private ObservableCollection<StorageItem> _storageBreakdown = new();
    public InsightsViewModel(InsightsService insights) { _insights = insights; }

    [RelayCommand]
    private void Load()
    {
        GpsCount = _insights.GetGpsCount(); FavoriteCount = _insights.GetFavoriteCount(); AvgQuality = _insights.GetQualityStats().avg;
        TopCameras = new(_insights.GetTopCameras().Select(c => new NameCount(c.camera, c.count)));
        TopLocations = new(_insights.GetTopLocations().Select(l => new NameCount(l.location, l.count)));
        TopFormats = new(_insights.GetTopFormats().Select(f => new NameCount(f.ext, f.count)));
        StorageBreakdown = new(_insights.GetStorageBreakdown().Select(s => new StorageItem(s.type, s.count, s.size)));
    }
}
public class NameCount { public string Name { get; set; } public int Count { get; set; } public NameCount(string n, int c) { Name = n; Count = c; } }
public class StorageItem { public string Type { get; set; } public int Count { get; set; } public long Size { get; set; } public string SizeDisplay => Size > 1024L * 1024 * 1024 ? $"{Size / (1024.0 * 1024 * 1024):N1} GB" : $"{Size / (1024.0 * 1024):N0} MB"; public StorageItem(string t, int c, long s) { Type = t; Count = c; Size = s; } }
