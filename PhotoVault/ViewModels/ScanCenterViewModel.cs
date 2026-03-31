using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PhotoVault.Services;

namespace PhotoVault.ViewModels;

public partial class ScanCenterViewModel : ObservableObject
{
    private readonly ScanPipelineService _pipeline;
    [ObservableProperty] private ObservableCollection<ScanStep> _steps = new();
    [ObservableProperty] private bool _isRunning, _isPaused;
    [ObservableProperty] private string _statusText = "Ready";
    public GalleryViewModel? GalleryVm { get; set; }

    public ScanCenterViewModel(ScanPipelineService pipeline, LogService log)
    {
        _pipeline = pipeline;
        _pipeline.StepsUpdated += () => System.Windows.Application.Current?.Dispatcher.Invoke(() => { Steps = new(_pipeline.Steps); IsRunning = _pipeline.IsRunning; IsPaused = _pipeline.IsPaused; });
        _pipeline.StatusChanged += s => System.Windows.Application.Current?.Dispatcher.Invoke(() => StatusText = s);
    }

    [RelayCommand] private void LoadSteps() { Steps = new(_pipeline.Steps); }
    [RelayCommand] private async Task RunAllAsync() { IsRunning = true; StatusText = "Running..."; await _pipeline.RunAllAsync(); IsRunning = false; StatusText = "Complete"; RefreshGallery(); }
    [RelayCommand] private async Task RunStepAsync(string? id) { if (string.IsNullOrEmpty(id)) return; IsRunning = true; await _pipeline.RunStepAsync(id); IsRunning = false; RefreshGallery(); }
    [RelayCommand] private void Pause() { _pipeline.Pause(); IsPaused = true; }
    [RelayCommand] private void Resume() { _pipeline.Resume(); IsPaused = false; }
    [RelayCommand] private void Cancel() { _pipeline.Cancel(); IsRunning = false; IsPaused = false; StatusText = "Cancelled"; }
    private void RefreshGallery() { System.Windows.Application.Current?.Dispatcher.Invoke(() => GalleryVm?.LoadMediaCommand.Execute(null)); }
}
