using System.Reflection;
using PhotoVault.Core.Models;

namespace PhotoVault.Services;

public interface IPhotoVaultPlugin { string Name { get; } string Description { get; } string Version { get; } string Author { get; } void Initialize(); void Dispose(); }

public class PluginService
{
    private readonly LogService _log; private readonly string _pluginDir;
    private readonly List<PluginInfo> _loaded = new();
    public IReadOnlyList<PluginInfo> LoadedPlugins => _loaded;
    public PluginService(LogService log, string pluginDir) { _log = log; _pluginDir = pluginDir; Directory.CreateDirectory(pluginDir); }

    public void LoadPlugins()
    {
        _loaded.Clear(); if (!Directory.Exists(_pluginDir)) return;
        foreach (var dll in Directory.GetFiles(_pluginDir, "*.dll"))
        {
            try
            {
                var asm = Assembly.LoadFrom(dll);
                foreach (var type in asm.GetTypes().Where(t => typeof(IPhotoVaultPlugin).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract))
                {
                    try
                    {
                        var inst = Activator.CreateInstance(type) as IPhotoVaultPlugin;
                        if (inst != null) { inst.Initialize(); _loaded.Add(new PluginInfo { Name = inst.Name, Description = inst.Description, Version = inst.Version, Author = inst.Author, FilePath = dll, IsEnabled = true, Instance = inst }); _log.Info("Plugins", $"Loaded: {inst.Name}"); }
                    } catch (Exception ex) { _log.Error("Plugins", $"Failed {type.Name}: {ex.Message}"); }
                }
            } catch { }
        }
        _log.Info("Plugins", $"{_loaded.Count} plugins loaded");
    }

    public void UnloadAll() { foreach (var p in _loaded) try { p.Instance?.Dispose(); } catch { } _loaded.Clear(); }
}

public class PluginInfo
{
    public string Name { get; set; } = ""; public string Description { get; set; } = ""; public string Version { get; set; } = "";
    public string Author { get; set; } = ""; public string FilePath { get; set; } = ""; public bool IsEnabled { get; set; }
    public IPhotoVaultPlugin? Instance { get; set; }
}
