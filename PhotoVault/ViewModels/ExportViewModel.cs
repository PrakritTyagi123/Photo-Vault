using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PhotoVault.Services;

namespace PhotoVault.ViewModels;

public partial class ExportViewModel : ObservableObject
{
    private readonly ExportService _export;
    [ObservableProperty] private string _statusText = "";
    public ExportViewModel(ExportService export) { _export = export; }
    [RelayCommand] private async Task ExportAllAsync() { var dlg = new System.Windows.Forms.FolderBrowserDialog { Description = "Export destination" }; if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK) return; StatusText = "Select albums from Albums page to export"; }
}
