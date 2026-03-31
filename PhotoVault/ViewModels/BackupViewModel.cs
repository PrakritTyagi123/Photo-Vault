using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PhotoVault.Services;

namespace PhotoVault.ViewModels;

public partial class BackupViewModel : ObservableObject
{
    private readonly BackupService _backup;
    private readonly string _backupDir;
    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private ObservableCollection<BackupInfo> _backups = new();
    public BackupViewModel(BackupService backup) { _backup = backup; _backupDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PhotoVault", "backups"); }
    [RelayCommand] private void Load() { Backups = new(_backup.GetExistingBackups(_backupDir)); }
    [RelayCommand] private async Task CreateBackupAsync() { StatusText = "Creating backup..."; var p = new Progress<string>(s => StatusText = s); var path = await _backup.CreateBackupAsync(_backupDir, true, p); StatusText = path != null ? $"Saved: {Path.GetFileName(path)}" : "Failed"; Load(); }
}
