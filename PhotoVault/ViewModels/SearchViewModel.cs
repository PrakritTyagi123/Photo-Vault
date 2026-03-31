using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PhotoVault.Core.Models;
using PhotoVault.Services;

namespace PhotoVault.ViewModels;

public partial class SearchViewModel : ObservableObject
{
    private readonly SearchService _search;
    private System.Timers.Timer? _debounce;

    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private string _selectedMonth = "", _selectedLocation = "", _selectedCamera = "", _selectedPerson = "", _selectedType = "", _selectedLens = "", _selectedVibe = "";
    [ObservableProperty] private ObservableCollection<MediaItem> _results = new();
    [ObservableProperty] private int _resultCount;
    [ObservableProperty] private bool _hasSearched, _isSearching;
    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private ObservableCollection<string> _locations = new(), _cameras = new(), _types = new(), _lenses = new(), _vibes = new(), _suggestions = new();
    public ViewerViewModel Viewer { get; }

    public SearchViewModel(SearchService search, ViewerViewModel viewer) { _search = search; Viewer = viewer; RefreshFilters(); Suggestions = new(new[] { "landscape", "portrait", "night", "food", "travel", "family" }); }

    public void RefreshFilters()
    {
        Locations = new(_search.GetDistinctValues("location")); Cameras = new(_search.GetDistinctValues("camera"));
        Types = new(_search.GetDistinctValues("type")); Lenses = new(_search.GetDistinctValues("lens")); Vibes = new(_search.GetDistinctValues("vibe"));
    }

    [RelayCommand]
    private void PerformSearch()
    {
        var q = new SearchQuery { Text = SearchText, Month = SelectedMonth, Location = SelectedLocation, Camera = SelectedCamera, Person = SelectedPerson, MediaType = SelectedType, Lens = SelectedLens, Vibe = SelectedVibe };
        if (!q.HasAnyFilter) { Results = new(); ResultCount = 0; HasSearched = false; StatusText = ""; return; }
        IsSearching = true; HasSearched = true;
        try { var items = _search.Search(q); Results = new(items); ResultCount = items.Count; StatusText = $"{ResultCount} results found"; }
        catch (Exception ex) { StatusText = $"Error: {ex.Message}"; Results = new(); ResultCount = 0; }
        IsSearching = false;
    }

    [RelayCommand] private void ApplyChip(string? c) { if (!string.IsNullOrEmpty(c)) { SearchText = c; PerformSearch(); } }
    [RelayCommand] private void ClearFilters() { SearchText = ""; SelectedMonth = ""; SelectedLocation = ""; SelectedCamera = ""; SelectedPerson = ""; SelectedType = ""; SelectedLens = ""; SelectedVibe = ""; Results = new(); ResultCount = 0; HasSearched = false; StatusText = ""; }
    [RelayCommand] private void OpenResult(MediaItem? item) { if (item != null) Viewer.Open(item, Results.ToList()); }

    public void OnFilterChanged() => PerformSearch();
    public void OnSearchTextChanged()
    {
        _debounce?.Stop(); _debounce?.Dispose();
        _debounce = new System.Timers.Timer(300) { AutoReset = false };
        _debounce.Elapsed += (s, e) => System.Windows.Application.Current?.Dispatcher.Invoke(PerformSearch);
        _debounce.Start();
    }
}
