using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PhotoVault.Core.Data;
using PhotoVault.Core.Models;
using PhotoVault.Services;

namespace PhotoVault.ViewModels;

public partial class ViewerViewModel : ObservableObject
{
    private readonly MediaRepository _repo;
    private VaultService? _vault;
    private List<MediaItem> _items = new();

    [ObservableProperty] private bool _isOpen;
    [ObservableProperty] private int _currentIndex;
    [ObservableProperty] private MediaItem? _currentItem;
    [ObservableProperty] private BitmapImage? _currentImage;
    [ObservableProperty] private string _fileName = "", _fileInfo = "", _positionText = "";
    [ObservableProperty] private bool _isEditPanelOpen, _isInfoPanelOpen, _isCinematicMode;
    [ObservableProperty] private double _editBrightness, _editContrast, _editSaturation, _editSharpen, _editVignette;
    [ObservableProperty] private string _editFilter = "None";
    [ObservableProperty] private ObservableCollection<MetadataRow> _metadataRows = new();
    [ObservableProperty] private int _starRating;
    [ObservableProperty] private bool _isFavorite;
    [ObservableProperty] private string _star1 = "\u2606", _star2 = "\u2606", _star3 = "\u2606", _star4 = "\u2606", _star5 = "\u2606";
    [ObservableProperty] private bool _isVideo, _isPlaying, _isMuted;
    [ObservableProperty] private Uri? _videoSource;
    [ObservableProperty] private double _videoPosition, _videoDuration = 1, _videoVolume = 0.8, _playbackSpeed = 1.0;
    [ObservableProperty] private string _currentTimeText = "0:00", _totalTimeText = "0:00", _playIcon = "\u25B6", _volumeIcon = "\uD83D\uDD0A", _speedLabel = "1x";

    public event Action? PlayRequested;
    public event Action? PauseRequested;
    public event Action<double>? SeekRequested;
    public event Action<double>? SpeedChangeRequested;

    public ViewerViewModel(MediaRepository repo) { _repo = repo; }
    public void SetVaultService(VaultService vault) { _vault = vault; }

    public void Open(MediaItem item, List<MediaItem> items)
    {
        _items = items; CurrentIndex = _items.IndexOf(item); if (CurrentIndex < 0) CurrentIndex = 0;
        ResetEdits(); IsEditPanelOpen = false; IsInfoPanelOpen = false; IsCinematicMode = false;
        LoadCurrent(); IsOpen = true;
    }

    [RelayCommand] private void Close() { StopVideo(); IsOpen = false; IsCinematicMode = false; IsEditPanelOpen = false; IsInfoPanelOpen = false; CurrentImage = null; VideoSource = null; }
    [RelayCommand] private void Previous() { if (CurrentIndex > 0) { StopVideo(); CurrentIndex--; LoadCurrent(); } }
    [RelayCommand] private void Next() { if (CurrentIndex < _items.Count - 1) { StopVideo(); CurrentIndex++; LoadCurrent(); } }
    [RelayCommand] private void ToggleEditPanel() { IsEditPanelOpen = !IsEditPanelOpen; if (IsEditPanelOpen) IsInfoPanelOpen = false; }
    [RelayCommand] private void ToggleInfoPanel() { IsInfoPanelOpen = !IsInfoPanelOpen; if (IsInfoPanelOpen) IsEditPanelOpen = false; }
    [RelayCommand] private void ToggleCinematic() { IsCinematicMode = !IsCinematicMode; }
    [RelayCommand] private void SetFilter(string? f) { if (!string.IsNullOrEmpty(f)) EditFilter = f; }
    [RelayCommand] private void ResetEdits() { EditBrightness = 0; EditContrast = 0; EditSaturation = 0; EditSharpen = 0; EditVignette = 0; EditFilter = "None"; }

    [RelayCommand]
    private void SetRating(string? r)
    {
        if (CurrentItem == null || !int.TryParse(r, out var rating)) return;
        if (StarRating == rating) rating = 0;
        StarRating = rating; UpdateStars(); _repo.UpdateRating(CurrentItem.Id, rating); CurrentItem.StarRating = rating;
    }

    [RelayCommand]
    private void ToggleFavorite() { if (CurrentItem == null) return; IsFavorite = !IsFavorite; _repo.UpdateFavorite(CurrentItem.Id, IsFavorite); CurrentItem.IsFavorite = IsFavorite; }

    [RelayCommand]
    private void MoveToVault()
    {
        if (CurrentItem == null || _vault == null || !_vault.IsUnlocked) return;
        _vault.MoveToVault(CurrentItem.Id);
        if (CurrentIndex < _items.Count - 1) Next(); else if (CurrentIndex > 0) Previous(); else Close();
    }

    private void UpdateStars() { Star1 = StarRating >= 1 ? "\u2605" : "\u2606"; Star2 = StarRating >= 2 ? "\u2605" : "\u2606"; Star3 = StarRating >= 3 ? "\u2605" : "\u2606"; Star4 = StarRating >= 4 ? "\u2605" : "\u2606"; Star5 = StarRating >= 5 ? "\u2605" : "\u2606"; }

    [RelayCommand] private void TogglePlay() { if (!IsVideo) return; IsPlaying = !IsPlaying; PlayIcon = IsPlaying ? "\u23F8" : "\u25B6"; if (IsPlaying) PlayRequested?.Invoke(); else PauseRequested?.Invoke(); }
    [RelayCommand] private void ToggleMute() { IsMuted = !IsMuted; VolumeIcon = IsMuted ? "\uD83D\uDD07" : "\uD83D\uDD0A"; }
    [RelayCommand] private void SetSpeed(string? s) { if (double.TryParse(s, System.Globalization.CultureInfo.InvariantCulture, out var v)) { PlaybackSpeed = v; SpeedLabel = v == 1 ? "1x" : v + "x"; SpeedChangeRequested?.Invoke(v); } }
    [RelayCommand] private void SkipBack() { if (!IsVideo) return; var p = Math.Max(0, VideoPosition - 10); VideoPosition = p; SeekRequested?.Invoke(p); }
    [RelayCommand] private void SkipForward() { if (!IsVideo) return; var p = Math.Min(VideoDuration, VideoPosition + 10); VideoPosition = p; SeekRequested?.Invoke(p); }
    [RelayCommand] private void FrameBack() { if (!IsVideo) return; PauseRequested?.Invoke(); IsPlaying = false; PlayIcon = "\u25B6"; var p = Math.Max(0, VideoPosition - 0.033); VideoPosition = p; SeekRequested?.Invoke(p); }
    [RelayCommand] private void FrameForward() { if (!IsVideo) return; PauseRequested?.Invoke(); IsPlaying = false; PlayIcon = "\u25B6"; var p = Math.Min(VideoDuration, VideoPosition + 0.033); VideoPosition = p; SeekRequested?.Invoke(p); }
    [RelayCommand] private void VolumeUp() { VideoVolume = Math.Min(1, VideoVolume + 0.1); IsMuted = false; VolumeIcon = "\uD83D\uDD0A"; }
    [RelayCommand] private void VolumeDown() { VideoVolume = Math.Max(0, VideoVolume - 0.1); }

    public void UpdateVideoPosition(double pos, double dur) { VideoPosition = pos; if (dur > 0) VideoDuration = dur; CurrentTimeText = Fmt(pos); TotalTimeText = Fmt(dur); }
    public void OnSeekBarChanged(double pos) { VideoPosition = pos; SeekRequested?.Invoke(pos); }
    private static string Fmt(double s) { var t = TimeSpan.FromSeconds(s); return t.TotalHours >= 1 ? t.ToString(@"h\:mm\:ss") : t.ToString(@"m\:ss"); }
    private void StopVideo() { if (IsPlaying) PauseRequested?.Invoke(); IsPlaying = false; PlayIcon = "\u25B6"; }

    private void LoadCurrent()
    {
        if (CurrentIndex < 0 || CurrentIndex >= _items.Count) return;
        CurrentItem = _items[CurrentIndex];
        var full = _repo.GetById(CurrentItem.Id); if (full != null) CurrentItem = full;
        FileName = CurrentItem.FileName; PositionText = $"{CurrentIndex + 1} / {_items.Count}";
        FileInfo = $"{CurrentItem.FileSizeDisplay}  |  {CurrentItem.DisplayDate:dd MMM yyyy}  |  {CurrentItem.MediaType}";
        StarRating = CurrentItem.StarRating; IsFavorite = CurrentItem.IsFavorite; UpdateStars();
        IsVideo = CurrentItem.MediaType == MediaType.Video || CurrentItem.MediaType == MediaType.SlowMotion || CurrentItem.MediaType == MediaType.Gif;
        if (IsVideo) { CurrentImage = null; VideoSource = File.Exists(CurrentItem.FilePath) ? new Uri(CurrentItem.FilePath) : null; VideoPosition = 0; VideoDuration = 1; CurrentTimeText = "0:00"; TotalTimeText = "0:00"; IsPlaying = false; PlayIcon = "\u25B6"; PlaybackSpeed = CurrentItem.MediaType == MediaType.SlowMotion ? 0.25 : 1; SpeedLabel = PlaybackSpeed == 1 ? "1x" : PlaybackSpeed + "x"; }
        else { VideoSource = null; IsPlaying = false; PlayIcon = "\u25B6"; LoadImage(); }
        BuildMeta();
    }

    private void LoadImage()
    {
        if (CurrentItem == null) return;
        try { var p = CurrentItem.FilePath; if (!File.Exists(p)) p = CurrentItem.ThumbnailLarge; if (string.IsNullOrEmpty(p) || !File.Exists(p)) { CurrentImage = null; return; } var b = new BitmapImage(); b.BeginInit(); b.UriSource = new Uri(p, UriKind.Absolute); b.CacheOption = BitmapCacheOption.OnLoad; b.CreateOptions = BitmapCreateOptions.IgnoreColorProfile; b.EndInit(); b.Freeze(); CurrentImage = b; }
        catch { CurrentImage = null; }
    }

    private void BuildMeta()
    {
        if (CurrentItem == null) return; var p = CurrentItem;
        MetadataRows = new ObservableCollection<MetadataRow> {
            new("File", p.FileName), new("Format", p.FileExtension.TrimStart('.').ToUpperInvariant()), new("Size", p.FileSizeDisplay),
            new("Dimensions", p.Width.HasValue && p.Height.HasValue ? $"{p.Width} x {p.Height}" : "—"),
            new("Date Taken", p.DateTaken.HasValue ? p.DateTaken.Value.ToString("dd MMM yyyy, HH:mm") : "—"),
            new("Camera", p.CameraModel ?? "—"), new("Lens", p.LensModel ?? "—"),
            new("ISO", p.Iso.HasValue ? p.Iso.Value.ToString() : "—"), new("Aperture", p.Aperture ?? "—"),
            new("Shutter", p.ShutterSpeed ?? "—"), new("Focal Length", p.FocalLength.HasValue ? $"{p.FocalLength:N0}mm" : "—"),
            new("GPS", p.Latitude.HasValue && p.Longitude.HasValue ? $"{p.Latitude:F4}, {p.Longitude:F4}" : "—"),
            new("City", !string.IsNullOrEmpty(p.City) ? p.City : "—"), new("Country", !string.IsNullOrEmpty(p.Country) ? p.Country : "—"),
            new("Address", !string.IsNullOrEmpty(p.Address) ? p.Address : "—"),
            new("Quality", p.QualityScore.HasValue ? $"{p.QualityScore:N0}/100" : "—"), new("Vibe", p.Vibe ?? "—"),
            new("Rating", p.StarRating > 0 ? new string('\u2605', p.StarRating) : "—"),
        };
    }
}

public class MetadataRow { public string Key { get; set; } public string Value { get; set; } public MetadataRow(string k, string v) { Key = k; Value = v; } }
