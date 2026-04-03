using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PhotoVault.Services;

namespace PhotoVault.ViewModels;

public partial class PluginsViewModel : ObservableObject
{
    private readonly PluginService _plugins;
    [ObservableProperty] private ObservableCollection<PluginInfo> _plugins2 = new();
    [ObservableProperty] private string _pluginPath;
    public PluginsViewModel(PluginService plugins) { _plugins = plugins; PluginPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PhotoVault", "plugins"); }
    [RelayCommand] private void Load() { Plugins2 = new(_plugins.LoadedPlugins); }
    [RelayCommand] private void OpenFolder() { Directory.CreateDirectory(_pluginPath); Process.Start("explorer.exe", _pluginPath); }
}
