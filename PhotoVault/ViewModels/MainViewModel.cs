using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace PhotoVault.ViewModels;

public partial class MainViewModel : ObservableObject
{
    [ObservableProperty] private string _currentView = "Gallery";
    [ObservableProperty] private int _mediaCount;
    [ObservableProperty] private string _statusText = "Ready";

    [ObservableProperty] private GalleryViewModel _galleryVm = null!;
    [ObservableProperty] private SearchViewModel _searchVm = null!;
    [ObservableProperty] private AlbumViewModel _albumVm = null!;
    [ObservableProperty] private ScanCenterViewModel _scanVm = null!;
    [ObservableProperty] private SettingsViewModel _settingsVm = null!;
    [ObservableProperty] private LogViewModel _logVm = null!;
    [ObservableProperty] private HealthViewModel _healthVm = null!;
    [ObservableProperty] private InsightsViewModel _insightsVm = null!;
    [ObservableProperty] private CleanupViewModel _cleanupVm = null!;
    [ObservableProperty] private TripsViewModel _tripsVm = null!;
    [ObservableProperty] private VaultViewModel _vaultVm = null!;
    [ObservableProperty] private ExportViewModel _exportVm = null!;
    [ObservableProperty] private BackupViewModel _backupVm = null!;
    [ObservableProperty] private PluginsViewModel _pluginsVm = null!;
    [ObservableProperty] private MapViewModel _mapVm = null!;
    [ObservableProperty] private PeopleViewModel _peopleVm = null!;

    [RelayCommand]
    private void Navigate(string? view) { if (!string.IsNullOrEmpty(view)) CurrentView = view; }
}
