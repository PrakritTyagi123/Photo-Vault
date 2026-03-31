using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PhotoVault.Services;

namespace PhotoVault.ViewModels;

public partial class LogViewModel : ObservableObject
{
    private readonly LogService _log;
    [ObservableProperty] private ObservableCollection<LogEntry> _entries = new();
    [ObservableProperty] private string _filterLevel = "All";

    public LogViewModel(LogService log) { _log = log; }

    [RelayCommand] private void LoadEntries() { Entries = new(_log.GetFiltered(FilterLevel)); }
    [RelayCommand] private void SetLevel(string? level) { if (!string.IsNullOrEmpty(level)) { FilterLevel = level; LoadEntries(); } }
    [RelayCommand] private void Clear() { _log.Clear(); Entries.Clear(); }
}
