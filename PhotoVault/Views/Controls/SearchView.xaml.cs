using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PhotoVault.Core.Models;
using PhotoVault.ViewModels;

namespace PhotoVault.Views.Controls;

public partial class SearchView : UserControl
{
    public SearchView() { InitializeComponent(); }
    private SearchViewModel? Vm => DataContext as SearchViewModel;
    public void PopulateFilters() { if (Vm == null) return; Fill(cbLoc, Vm.Locations, "Location"); Fill(cbCam, Vm.Cameras, "Camera"); Fill(cbType, Vm.Types, "Type"); Fill(cbLens, Vm.Lenses, "Lens"); Fill(cbVibe, Vm.Vibes, "Vibe"); }
    private static void Fill(ComboBox cb, System.Collections.IEnumerable items, string ph) { cb.Items.Clear(); cb.Items.Add(new ComboBoxItem { Content = ph, Tag = "" }); foreach (string i in items) cb.Items.Add(new ComboBoxItem { Content = i, Tag = i }); cb.SelectedIndex = 0; }
    private void Search_Changed(object sender, TextChangedEventArgs e) { if (Vm != null && sender is TextBox tb) { Vm.SearchText = tb.Text; Vm.OnSearchTextChanged(); } }
    private void Filter_Changed(object sender, SelectionChangedEventArgs e) { if (Vm == null || sender is not ComboBox cb || cb.SelectedItem is not ComboBoxItem item || item.Tag is not string val) return; switch (cb.Tag as string) { case "month": Vm.SelectedMonth = val; break; case "location": Vm.SelectedLocation = val; break; case "camera": Vm.SelectedCamera = val; break; case "type": Vm.SelectedType = val; break; case "lens": Vm.SelectedLens = val; break; case "vibe": Vm.SelectedVibe = val; break; } Vm.OnFilterChanged(); }
    private void Chip_Click(object sender, MouseButtonEventArgs e) { if (sender is FrameworkElement fe && fe.DataContext is string chip) Vm?.ApplyChipCommand.Execute(chip); }
    private void Result_Click(object sender, MouseButtonEventArgs e) { if (sender is FrameworkElement fe && fe.DataContext is MediaItem item) Vm?.OpenResultCommand.Execute(item); }
}
