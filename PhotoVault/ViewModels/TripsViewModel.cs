using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PhotoVault.Services;

namespace PhotoVault.ViewModels;

public partial class TripsViewModel : ObservableObject
{
    private readonly TripDetectionService _trips;
    [ObservableProperty] private ObservableCollection<TripInfo> _trips2 = new();
    [ObservableProperty] private string _statusText = "";
    public TripsViewModel(TripDetectionService trips) { _trips = trips; }
    [RelayCommand] private void Load() { Trips2 = new(_trips.GetAllTrips()); }
    [RelayCommand] private void Detect() { StatusText = "Analyzing GPS..."; var d = _trips.DetectTrips(); var s = _trips.SaveTrips(d); StatusText = $"Found {d.Count} trips, saved {s} new"; Trips2 = new(_trips.GetAllTrips()); }
}
