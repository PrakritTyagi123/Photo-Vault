using System.Windows;
using System.Windows.Controls;
using PhotoVault.ViewModels;

namespace PhotoVault.Views.Controls;

public partial class ScanCenterView : UserControl
{
    public ScanCenterView() { InitializeComponent(); }
    private ScanCenterViewModel? Vm => DataContext as ScanCenterViewModel;
    private async void RunStep_Click(object sender, RoutedEventArgs e) { if (sender is Button btn && btn.Tag is string id && Vm != null) await Vm.RunStepCommand.ExecuteAsync(id); }
}
