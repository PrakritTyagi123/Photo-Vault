using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PhotoVault.Core.Data;
using PhotoVault.Core.Models;
using PhotoVault.Services;

namespace PhotoVault.ViewModels;

public partial class GalleryViewModel : ObservableObject
{
    private readonly MediaRepository _repo;
    private readonly FileDiscoveryService _fileService;
    private readonly AlbumRepository _albumRepo;
    private readonly LogService _log;

    [ObservableProperty] private ObservableCollection<MediaItem> _mediaItems = new();
    [ObservableProperty] private int _totalCount;
    [ObservableProperty] private bool _isScanning;
    [ObservableProperty] private string _scanStatus = "";
    [ObservableProperty] private string _viewMode = "grid";
    [ObservableProperty] private string _sortMode = "date-desc";

    public ICollectionView? GroupedItems { get; private set; }
    public ViewerViewModel Viewer { get; }
    public event Action? ImportCompleted;

    public GalleryViewModel(MediaRepository repo, FileDiscoveryService fileService, AlbumRepository albumRepo, LogService log, ViewerViewModel viewer)
    {
        _repo = repo; _fileService = fileService; _albumRepo = albumRepo; _log = log; Viewer = viewer;
    }

    [RelayCommand]
    private void LoadMedia()
    {
        var items = _repo.GetAll();
        MediaItems = new ObservableCollection<MediaItem>(items);
        TotalCount = items.Count;
        GroupedItems = CollectionViewSource.GetDefaultView(MediaItems);
        GroupedItems.GroupDescriptions.Clear();
        GroupedItems.GroupDescriptions.Add(new PropertyGroupDescription("MonthGroup"));
        OnPropertyChanged(nameof(GroupedItems));
    }

    [RelayCommand]
    private async Task ImportFolderAsync(string? folderPath)
    {
        if (string.IsNullOrEmpty(folderPath)) return;
        IsScanning = true; ScanStatus = "Discovering files...";
        _log.Info("Import", $"Scanning: {folderPath}");

        var progress = new Progress<int>(c => ScanStatus = $"Discovering files... {c} found");
        int found = 0;
        try { found = await Task.Run(() => _fileService.ScanFolderAsync(folderPath, progress)); }
        catch (Exception ex) { ScanStatus = $"Error: {ex.Message}"; IsScanning = false; return; }

        if (_fileService.SubfolderMap.Count > 0)
        {
            ScanStatus = "Creating albums...";
            foreach (var (name, ids) in _fileService.SubfolderMap)
            {
                var aid = _albumRepo.CreateAlbum(name, AlbumType.Manual);
                if (aid > 0) { _albumRepo.AddMediaBatch(aid, ids); if (ids.Count > 0) _albumRepo.SetCover(aid, ids[0]); }
            }
        }

        ScanStatus = found > 0 ? $"Added {found} files. Go to Scan Center to process." : "No new files found";
        IsScanning = false; LoadMedia(); ImportCompleted?.Invoke();
        _ = Task.Run(async () => { await Task.Delay(5000); System.Windows.Application.Current?.Dispatcher.Invoke(() => { if (!IsScanning) ScanStatus = ""; }); });
    }

    [RelayCommand] private void SetViewMode(string? m) { if (!string.IsNullOrEmpty(m)) ViewMode = m; }

    [RelayCommand]
    private void SetSort(string? sort)
    {
        if (string.IsNullOrEmpty(sort) || GroupedItems == null) return;
        SortMode = sort; GroupedItems.SortDescriptions.Clear();
        switch (sort)
        {
            case "date-desc": GroupedItems.SortDescriptions.Add(new SortDescription("DisplayDate", ListSortDirection.Descending)); break;
            case "date-asc": GroupedItems.SortDescriptions.Add(new SortDescription("DisplayDate", ListSortDirection.Ascending)); break;
            case "name": GroupedItems.SortDescriptions.Add(new SortDescription("FileName", ListSortDirection.Ascending)); break;
            case "size": GroupedItems.SortDescriptions.Add(new SortDescription("FileSize", ListSortDirection.Descending)); break;
        }
        GroupedItems.Refresh();
    }

    [RelayCommand]
    private void OpenViewer(MediaItem? item) { if (item != null) Viewer.Open(item, MediaItems.ToList()); }
}
