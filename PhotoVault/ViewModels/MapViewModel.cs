using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PhotoVault.Services;

namespace PhotoVault.ViewModels;

public partial class MapViewModel : ObservableObject
{
    private readonly MapService _map;
    [ObservableProperty] private int _gpsCount;
    [ObservableProperty] private ObservableCollection<MapCluster> _clusters = new();
    [ObservableProperty] private string _mapHtml = "";
    public MapViewModel(MapService map) { _map = map; }
    [RelayCommand] private void Load() { GpsCount = _map.GetGpsCount(); Clusters = new(_map.GetClusters()); MapHtml = _map.GenerateMapHtml(); }
}
