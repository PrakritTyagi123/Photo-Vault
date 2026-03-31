using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PhotoVault.Core.Models;
using PhotoVault.Services;

namespace PhotoVault.ViewModels;

public partial class CleanupViewModel : ObservableObject
{
    private readonly CleanupService _cleanup;
    [ObservableProperty] private int _duplicateCount, _largeFileCount, _lowQualityCount;
    [ObservableProperty] private ObservableCollection<DuplicateGroup> _duplicates = new();
    [ObservableProperty] private ObservableCollection<MediaItem> _largeFiles = new(), _lowQualityFiles = new();
    public CleanupViewModel(CleanupService cleanup) { _cleanup = cleanup; }
    [RelayCommand] private void Load() { var s = _cleanup.GetSummary(); DuplicateCount = s.DuplicateGroups; LargeFileCount = s.LargeFiles; LowQualityCount = s.LowQualityFiles; Duplicates = new(_cleanup.GetDuplicates()); LargeFiles = new(_cleanup.GetLargeFiles()); LowQualityFiles = new(_cleanup.GetLowQuality()); }
}
