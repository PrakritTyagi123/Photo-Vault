using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PhotoVault.Core.Models;
using PhotoVault.ViewModels;

namespace PhotoVault.Views.Controls;

public partial class GalleryView : UserControl
{
    public GalleryView() { InitializeComponent(); }
    private GalleryViewModel? Vm => DataContext as GalleryViewModel;
    private void Sort_Changed(object sender, SelectionChangedEventArgs e) { if (Vm != null && sender is ComboBox cb && cb.SelectedItem is ComboBoxItem item && item.Tag is string sort) Vm.SetSortCommand.Execute(sort); }
    private void Card_Click(object sender, MouseButtonEventArgs e) { if (sender is FrameworkElement fe && fe.DataContext is MediaItem item) Vm?.OpenViewerCommand.Execute(item); }
    private void GoToSettings_Click(object sender, RoutedEventArgs e) { if (System.Windows.Window.GetWindow(this)?.DataContext is MainViewModel main) main.NavigateCommand.Execute("Settings"); }
}
