using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PhotoVault.Services;

namespace PhotoVault.ViewModels;

public partial class HealthViewModel : ObservableObject
{
    private readonly HealthService _health;
    [ObservableProperty] private int _healthScore, _totalItems, _brokenFiles;
    [ObservableProperty] private bool _hasBrokenFiles;
    [ObservableProperty] private ObservableCollection<HealthItem> _healthItems = new();
    public HealthViewModel(HealthService health) { _health = health; }

    [RelayCommand]
    private void Load()
    {
        var r = _health.GenerateReport(); HealthScore = r.HealthScore; TotalItems = r.TotalItems; BrokenFiles = r.BrokenFiles; HasBrokenFiles = r.BrokenFiles > 0;
        HealthItems = new(new HealthItem[] { new("Thumbnails", "Preview images", r.MissingThumbnails), new("EXIF Data", "Camera/date/GPS", r.MissingExif), new("Date Taken", "Capture date", r.MissingDateTaken), new("GPS", "Coordinates", r.MissingGps), new("Geocoding", "City/country", r.MissingGeocode), new("Quality Score", "Sharpness rating", r.MissingQuality), new("File Hash", "SHA-256 hash", r.MissingHash), new("AI Tags", "Object labels", r.MissingTags), new("Faces", "Detected faces", r.MissingFaces) });
    }

    [RelayCommand] private void RemoveBroken() { _health.RemoveBrokenEntries(); Load(); }
}
public class HealthItem { public string Label { get; set; } public string Description { get; set; } public int MissingCount { get; set; } public HealthItem(string l, string d, int m) { Label = l; Description = d; MissingCount = m; } }
