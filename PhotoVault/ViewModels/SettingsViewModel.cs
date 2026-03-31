using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PhotoVault.Services;

namespace PhotoVault.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly SettingsService _settings;
    private readonly AiSidecarService _aiService;
    private readonly LogService _log;

    [ObservableProperty] private ObservableCollection<WatchedFolderInfo> _watchedFolders = new();
    [ObservableProperty] private bool _autoScanStartup, _autoDetectDrives, _sha256Tracking;
    [ObservableProperty] private int _maxConcurrentTasks;
    [ObservableProperty] private string _ignorePatterns = "", _inferenceBackend = "", _importStatus = "", _aiStatus = "", _aiDownloadStatus = "";
    [ObservableProperty] private bool _isImporting, _isAiRunning, _isDownloadingModels;
    [ObservableProperty] private LibraryStats _libraryStats = new();

    public GalleryViewModel? GalleryVm { get; set; }

    public SettingsViewModel(SettingsService settings, AiSidecarService aiService, LogService log) { _settings = settings; _aiService = aiService; _log = log; }

    [RelayCommand]
    private void LoadSettings()
    {
        _settings.EnsureDefaults();
        AutoScanStartup = _settings.GetBool("auto_scan_startup"); AutoDetectDrives = _settings.GetBool("auto_detect_drives");
        Sha256Tracking = _settings.GetBool("sha256_tracking"); MaxConcurrentTasks = _settings.GetInt("max_concurrent_tasks", 4);
        IgnorePatterns = _settings.Get("ignore_patterns") ?? ""; InferenceBackend = _settings.Get("inference_backend") ?? "Not configured";
        WatchedFolders = new(_settings.GetWatchedFolders()); LibraryStats = _settings.GetLibraryStats();
        IsAiRunning = _aiService.IsRunning; AiStatus = _aiService.Status;
    }

    [RelayCommand]
    private async Task AddFolderAsync()
    {
        var dlg = new System.Windows.Forms.FolderBrowserDialog { Description = "Select folder to import", ShowNewFolderButton = false, UseDescriptionForTitle = true };
        if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
        _settings.AddWatchedFolder(dlg.SelectedPath);
        if (GalleryVm != null)
        {
            IsImporting = true; ImportStatus = "Discovering files...";
            void OnChange(object? s, System.ComponentModel.PropertyChangedEventArgs e) { if (e.PropertyName == nameof(GalleryVm.ScanStatus)) ImportStatus = GalleryVm.ScanStatus; }
            GalleryVm.PropertyChanged += OnChange; await GalleryVm.ImportFolderCommand.ExecuteAsync(dlg.SelectedPath); GalleryVm.PropertyChanged -= OnChange;
            IsImporting = false; ImportStatus = "Files added. Go to Scan Center.";
            _ = Task.Run(async () => { await Task.Delay(5000); System.Windows.Application.Current?.Dispatcher.Invoke(() => ImportStatus = ""); });
        }
        WatchedFolders = new(_settings.GetWatchedFolders()); LibraryStats = _settings.GetLibraryStats();
    }

    [RelayCommand] private void RemoveFolder(WatchedFolderInfo? f) { if (f == null) return; _settings.RemoveWatchedFolder(f.Id); WatchedFolders = new(_settings.GetWatchedFolders()); }

    [RelayCommand]
    private async Task StartAiServerAsync()
    {
        AiStatus = "Starting..."; var ok = await _aiService.StartAsync(); IsAiRunning = _aiService.IsRunning; AiStatus = _aiService.Status;
    }

    [RelayCommand]
    private void StopAiServer() { _aiService.Stop(); IsAiRunning = false; AiStatus = "Stopped"; }

    [RelayCommand]
    private async Task DownloadModelsAsync()
    {
        IsDownloadingModels = true; AiDownloadStatus = "Downloading AI models...";
        var progress = new Progress<string>(s => AiDownloadStatus = s);
        await _aiService.DownloadModelsAsync(progress);
        IsDownloadingModels = false; AiDownloadStatus = "Download complete";
    }

    partial void OnAutoScanStartupChanged(bool value) => _settings.SetBool("auto_scan_startup", value);
    partial void OnAutoDetectDrivesChanged(bool value) => _settings.SetBool("auto_detect_drives", value);
    partial void OnSha256TrackingChanged(bool value) => _settings.SetBool("sha256_tracking", value);
}
