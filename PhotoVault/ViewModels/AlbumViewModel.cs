using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PhotoVault.Core.Data;
using PhotoVault.Core.Models;

namespace PhotoVault.ViewModels;

public partial class AlbumViewModel : ObservableObject
{
    private readonly AlbumRepository _albumRepo;
    private readonly MediaRepository _mediaRepo;
    public ViewerViewModel Viewer { get; }

    [ObservableProperty] private ObservableCollection<AlbumDisplayItem> _smartAlbums = new(), _manualAlbums = new();
    [ObservableProperty] private ObservableCollection<MediaItem> _currentAlbumMedia = new();
    [ObservableProperty] private AlbumDisplayItem? _selectedAlbum;
    [ObservableProperty] private bool _isViewingAlbum, _isCreateDialogOpen;
    [ObservableProperty] private string _currentAlbumName = "", _newAlbumName = "";
    [ObservableProperty] private int _currentAlbumCount;

    public AlbumViewModel(AlbumRepository albumRepo, MediaRepository mediaRepo, ViewerViewModel viewer) { _albumRepo = albumRepo; _mediaRepo = mediaRepo; Viewer = viewer; }

    [RelayCommand]
    private void LoadAlbums()
    {
        _albumRepo.EnsureDefaultAlbums(); _albumRepo.AutoSetAllCovers();
        var all = _albumRepo.GetAllAlbums(); var smart = new ObservableCollection<AlbumDisplayItem>(); var manual = new ObservableCollection<AlbumDisplayItem>();
        foreach (var a in all)
        {
            int count; string? cover;
            if (a.Type == AlbumType.Auto && !string.IsNullOrEmpty(a.SmartQuery)) { count = _albumRepo.GetSmartAlbumCount(a.SmartQuery); cover = _albumRepo.GetSmartAlbumCover(a.SmartQuery); }
            else { count = _albumRepo.GetAlbumMediaCount(a.Id); cover = _albumRepo.GetCoverThumbnail(a.Id); }
            var item = new AlbumDisplayItem { Id = a.Id, Name = a.Name, Type = a.Type, PhotoCount = count, CoverPath = cover, SmartQuery = a.SmartQuery };
            if (a.Type == AlbumType.Auto) smart.Add(item); else manual.Add(item);
        }
        SmartAlbums = smart; ManualAlbums = manual;
    }

    [RelayCommand]
    private void OpenAlbum(AlbumDisplayItem? a)
    {
        if (a == null) return; SelectedAlbum = a; CurrentAlbumName = a.Name;
        var media = a.Type == AlbumType.Auto && !string.IsNullOrEmpty(a.SmartQuery) ? _albumRepo.GetSmartAlbumMedia(a.SmartQuery) : _albumRepo.GetAlbumMedia(a.Id);
        CurrentAlbumMedia = new(media); CurrentAlbumCount = media.Count; IsViewingAlbum = true;
    }

    [RelayCommand] private void CloseAlbum() { IsViewingAlbum = false; SelectedAlbum = null; CurrentAlbumMedia.Clear(); }
    [RelayCommand] private void ShowCreateDialog() { NewAlbumName = ""; IsCreateDialogOpen = true; }
    [RelayCommand] private void CancelCreate() { IsCreateDialogOpen = false; NewAlbumName = ""; }
    [RelayCommand] private void ConfirmCreate() { if (string.IsNullOrWhiteSpace(NewAlbumName)) return; _albumRepo.CreateAlbum(NewAlbumName.Trim(), AlbumType.Manual); IsCreateDialogOpen = false; NewAlbumName = ""; LoadAlbums(); }
    [RelayCommand] private void DeleteAlbum(AlbumDisplayItem? a) { if (a == null || a.Type == AlbumType.Auto) return; _albumRepo.DeleteAlbum(a.Id); if (IsViewingAlbum && SelectedAlbum?.Id == a.Id) CloseAlbum(); LoadAlbums(); }
    [RelayCommand] private void OpenAlbumPhoto(MediaItem? item) { if (item != null) Viewer.Open(item, CurrentAlbumMedia.ToList()); }
}

public class AlbumDisplayItem { public long Id { get; set; } public string Name { get; set; } = ""; public AlbumType Type { get; set; } public int PhotoCount { get; set; } public string? CoverPath { get; set; } public string? SmartQuery { get; set; } public string CountText => $"{PhotoCount} items"; }
