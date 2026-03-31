using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PhotoVault.Core.Models;
using PhotoVault.Services;

namespace PhotoVault.ViewModels;

public partial class VaultViewModel : ObservableObject
{
    private readonly VaultService _vault;
    [ObservableProperty] private bool _isUnlocked, _needsPassword;
    [ObservableProperty] private string _errorText = "";
    [ObservableProperty] private int _vaultedCount;
    [ObservableProperty] private ObservableCollection<MediaItem> _vaultedItems = new();
    public VaultViewModel(VaultService vault) { _vault = vault; }
    [RelayCommand] private void Load() { NeedsPassword = !_vault.HasPassword(); IsUnlocked = _vault.IsUnlocked; if (IsUnlocked) Refresh(); }
    [RelayCommand] private void Unlock(string? pwd) { if (string.IsNullOrEmpty(pwd)) { ErrorText = "Enter password"; return; } if (_vault.Unlock(pwd)) { IsUnlocked = true; ErrorText = ""; Refresh(); } else ErrorText = "Wrong password"; }
    [RelayCommand] private void SetPassword(string? pwd) { if (string.IsNullOrEmpty(pwd) || pwd.Length < 4) { ErrorText = "Min 4 characters"; return; } _vault.SetPassword(pwd); IsUnlocked = true; NeedsPassword = false; ErrorText = ""; Refresh(); }
    [RelayCommand] private void Lock() { _vault.Lock(); IsUnlocked = false; VaultedItems.Clear(); VaultedCount = 0; }
    private void Refresh() { var items = _vault.GetVaultedItems(); VaultedItems = new(items); VaultedCount = items.Count; }
}
