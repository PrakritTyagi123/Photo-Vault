using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PhotoVault.Core.Models;
using PhotoVault.ViewModels;

namespace PhotoVault.Views.Controls;

public partial class AlbumsView : UserControl
{
    public AlbumsView() { InitializeComponent(); }
    private AlbumViewModel? Vm => DataContext as AlbumViewModel;
    private void Album_Click(object sender, MouseButtonEventArgs e) { if (sender is FrameworkElement fe && fe.DataContext is AlbumDisplayItem a) Vm?.OpenAlbumCommand.Execute(a); }
    private void AlbumPhoto_Click(object sender, MouseButtonEventArgs e) { if (sender is FrameworkElement fe && fe.DataContext is MediaItem item) Vm?.OpenAlbumPhotoCommand.Execute(item); }
    private void DeleteAlbum_Click(object sender, RoutedEventArgs e) { if (sender is FrameworkElement fe && fe.DataContext is AlbumDisplayItem a) { if (MessageBox.Show($"Delete \"{a.Name}\"?", "Confirm", MessageBoxButton.YesNo) == MessageBoxResult.Yes) Vm?.DeleteAlbumCommand.Execute(a); } }
}
