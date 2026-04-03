using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using PhotoVault.Services;
using PhotoVault.ViewModels;

namespace PhotoVault.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;
    private DispatcherTimer? _videoTimer;

    public MainWindow()
    {
        InitializeComponent();

        _vm = App.Services.GetRequiredService<MainViewModel>();
        _vm.GalleryVm = App.Services.GetRequiredService<GalleryViewModel>();
        _vm.SearchVm = App.Services.GetRequiredService<SearchViewModel>();
        _vm.AlbumVm = App.Services.GetRequiredService<AlbumViewModel>();
        _vm.ScanVm = App.Services.GetRequiredService<ScanCenterViewModel>();
        _vm.SettingsVm = App.Services.GetRequiredService<SettingsViewModel>();
        _vm.LogVm = App.Services.GetRequiredService<LogViewModel>();
        _vm.HealthVm = App.Services.GetRequiredService<HealthViewModel>();
        _vm.InsightsVm = App.Services.GetRequiredService<InsightsViewModel>();
        _vm.CleanupVm = App.Services.GetRequiredService<CleanupViewModel>();
        _vm.TripsVm = App.Services.GetRequiredService<TripsViewModel>();
        _vm.VaultVm = App.Services.GetRequiredService<VaultViewModel>();
        _vm.ExportVm = App.Services.GetRequiredService<ExportViewModel>();
        _vm.BackupVm = App.Services.GetRequiredService<BackupViewModel>();
        _vm.PluginsVm = App.Services.GetRequiredService<PluginsViewModel>();
        _vm.MapVm = App.Services.GetRequiredService<MapViewModel>();
        _vm.PeopleVm = App.Services.GetRequiredService<PeopleViewModel>();

        _vm.SettingsVm.GalleryVm = _vm.GalleryVm;
        _vm.ScanVm.GalleryVm = _vm.GalleryVm;
        _vm.GalleryVm.Viewer.SetVaultService(App.Services.GetRequiredService<VaultService>());

        DataContext = _vm;

        galleryView.DataContext = _vm.GalleryVm;
        searchView.DataContext = _vm.SearchVm;
        albumsView.DataContext = _vm.AlbumVm;
        scanView.DataContext = _vm.ScanVm;
        settingsView.DataContext = _vm.SettingsVm;
        logView.DataContext = _vm.LogVm;
        healthView.DataContext = _vm.HealthVm;
        insightsView.DataContext = _vm.InsightsVm;
        cleanupView.DataContext = _vm.CleanupVm;
        tripsView.DataContext = _vm.TripsVm;
        vaultView.DataContext = _vm.VaultVm;
        exportView.DataContext = _vm.ExportVm;
        backupView.DataContext = _vm.BackupVm;
        pluginsView.DataContext = _vm.PluginsVm;
        mapView.DataContext = _vm.MapVm;
        peopleView.DataContext = _vm.PeopleVm;

        _vm.GalleryVm.LoadMediaCommand.Execute(null);
        _vm.AlbumVm.LoadAlbumsCommand.Execute(null);
        _vm.ScanVm.LoadStepsCommand.Execute(null);
        _vm.SettingsVm.LoadSettingsCommand.Execute(null);
        _vm.LogVm.LoadEntriesCommand.Execute(null);

        _vm.MediaCount = _vm.GalleryVm.TotalCount;
        _vm.StatusText = _vm.GalleryVm.TotalCount > 0 ? $"{_vm.GalleryVm.TotalCount} items" : "Ready";

        _vm.GalleryVm.ImportCompleted += () =>
        {
            _vm.MediaCount = _vm.GalleryVm.TotalCount;
            _vm.StatusText = $"{_vm.GalleryVm.TotalCount} items";
            _vm.AlbumVm.LoadAlbumsCommand.Execute(null);
            _vm.ScanVm.LoadStepsCommand.Execute(null);
            _vm.SettingsVm.LoadSettingsCommand.Execute(null);
        };

        _vm.ScanVm.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == "IsRunning" && !_vm.ScanVm.IsRunning)
                Dispatcher.Invoke(() => { _vm.SearchVm.RefreshFilters(); searchView.PopulateFilters(); _vm.SettingsVm.LoadSettingsCommand.Execute(null); _vm.GalleryVm.LoadMediaCommand.Execute(null); _vm.MediaCount = _vm.GalleryVm.TotalCount; });
        };

        Loaded += (s, e) => searchView.PopulateFilters();
        WireVideo();
    }

    private void WireVideo()
    {
        var v = _vm.GalleryVm.Viewer;
        v.PlayRequested += () => { videoPlayer.Play(); StartTimer(); };
        v.PauseRequested += () => { videoPlayer.Pause(); StopTimer(); };
        v.SeekRequested += p => videoPlayer.Position = TimeSpan.FromSeconds(p);
        v.SpeedChangeRequested += s => videoPlayer.SpeedRatio = s;
        v.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(v.VideoSource))
                Dispatcher.Invoke(() => { if (v.VideoSource != null) { videoPlayer.Source = v.VideoSource; videoPlayer.Stop(); StopTimer(); } else { videoPlayer.Stop(); videoPlayer.Source = null; StopTimer(); } });
            if (e.PropertyName == nameof(v.IsOpen) && !v.IsOpen)
                Dispatcher.Invoke(() => { videoPlayer.Stop(); videoPlayer.Source = null; StopTimer(); });
        };
    }

    private void StartTimer() { _videoTimer?.Stop(); _videoTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) }; _videoTimer.Tick += (s, e) => { if (videoPlayer.NaturalDuration.HasTimeSpan) _vm.GalleryVm.Viewer.UpdateVideoPosition(videoPlayer.Position.TotalSeconds, videoPlayer.NaturalDuration.TimeSpan.TotalSeconds); }; _videoTimer.Start(); }
    private void StopTimer() { _videoTimer?.Stop(); _videoTimer = null; }
    private void Video_Opened(object sender, RoutedEventArgs e) { if (videoPlayer.NaturalDuration.HasTimeSpan) _vm.GalleryVm.Viewer.UpdateVideoPosition(0, videoPlayer.NaturalDuration.TimeSpan.TotalSeconds); videoPlayer.SpeedRatio = _vm.GalleryVm.Viewer.PlaybackSpeed; }
    private void Seek_Changed(object sender, RoutedPropertyChangedEventArgs<double> e) { if (Mouse.LeftButton == MouseButtonState.Pressed && videoPlayer.NaturalDuration.HasTimeSpan) _vm.GalleryVm.Viewer.OnSeekBarChanged(e.NewValue); }
    private void Volume_Changed(object sender, RoutedPropertyChangedEventArgs<double> e) { if (_vm?.GalleryVm?.Viewer != null) _vm.GalleryVm.Viewer.VideoVolume = e.NewValue; }
    private void Speed_Changed(object sender, SelectionChangedEventArgs e) { if (_vm?.GalleryVm?.Viewer == null) return; if (sender is ComboBox cb && cb.SelectedItem is ComboBoxItem item && item.Tag is string speed) _vm.GalleryVm.Viewer.SetSpeedCommand.Execute(speed); }
    private void Viewer_DoubleClick(object sender, MouseButtonEventArgs e) { if (e.ClickCount == 2) { _vm.GalleryVm.Viewer.ToggleCinematicCommand.Execute(null); e.Handled = true; } }

    private void Nav_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is string view)
        {
            _vm.NavigateCommand.Execute(view);
            switch (view)
            {
                case "Search": searchView.PopulateFilters(); break;
                case "Health": _vm.HealthVm.LoadCommand.Execute(null); break;
                case "Insights": _vm.InsightsVm.LoadCommand.Execute(null); break;
                case "Cleanup": _vm.CleanupVm.LoadCommand.Execute(null); break;
                case "Trips": _vm.TripsVm.LoadCommand.Execute(null); break;
                case "Vault": _vm.VaultVm.LoadCommand.Execute(null); break;
                case "Backup": _vm.BackupVm.LoadCommand.Execute(null); break;
                case "Plugins": _vm.PluginsVm.LoadCommand.Execute(null); break;
                case "Map": _vm.MapVm.LoadCommand.Execute(null); mapView.LoadMap(); break;
                case "Albums": _vm.AlbumVm.LoadAlbumsCommand.Execute(null); break;
                case "People": _vm.PeopleVm.LoadCommand.Execute(null); break;
                case "System Log": _vm.LogVm.LoadEntriesCommand.Execute(null); break;
            }
        }
    }

    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e) { if (e.ChangedButton == MouseButton.Left) { if (e.ClickCount == 2) { WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized; maxBtn.Content = WindowState == WindowState.Maximized ? "\u2750" : "\u25A1"; } else DragMove(); } }
    private void Min_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void Max_Click(object sender, RoutedEventArgs e) { WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized; maxBtn.Content = WindowState == WindowState.Maximized ? "\u2750" : "\u25A1"; }
    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (!_vm.GalleryVm.Viewer.IsOpen) return;
        switch (e.Key)
        {
            case Key.Left: _vm.GalleryVm.Viewer.PreviousCommand.Execute(null); e.Handled = true; break;
            case Key.Right: _vm.GalleryVm.Viewer.NextCommand.Execute(null); e.Handled = true; break;
            case Key.Space: if (_vm.GalleryVm.Viewer.IsVideo) { _vm.GalleryVm.Viewer.TogglePlayCommand.Execute(null); e.Handled = true; } break;
            case Key.Escape: if (_vm.GalleryVm.Viewer.IsCinematicMode) _vm.GalleryVm.Viewer.ToggleCinematicCommand.Execute(null); else _vm.GalleryVm.Viewer.CloseCommand.Execute(null); e.Handled = true; break;
            case Key.E: _vm.GalleryVm.Viewer.ToggleEditPanelCommand.Execute(null); e.Handled = true; break;
            case Key.I: _vm.GalleryVm.Viewer.ToggleInfoPanelCommand.Execute(null); e.Handled = true; break;
            case Key.F: _vm.GalleryVm.Viewer.ToggleCinematicCommand.Execute(null); e.Handled = true; break;
            case Key.M: _vm.GalleryVm.Viewer.ToggleMuteCommand.Execute(null); e.Handled = true; break;
        }
    }
}
