using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace PhotoVault.ViewModels;

public partial class PeopleViewModel : ObservableObject
{
    [ObservableProperty] private string _statusText = "Requires AI server to detect and group faces";
    [ObservableProperty] private ObservableCollection<PersonItem> _people = new();
    [RelayCommand] private void Load() { StatusText = "Start AI server from Settings, then run Face Detection from Scan Center"; }
}
public class PersonItem { public long Id { get; set; } public string Name { get; set; } = ""; public int FaceCount { get; set; } public string? ThumbnailPath { get; set; } }
