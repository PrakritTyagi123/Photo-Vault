using System.Windows.Controls;
using PhotoVault.ViewModels;

namespace PhotoVault.Views.Controls;

public partial class SystemLogView : UserControl
{
    public SystemLogView() { InitializeComponent(); }
    private LogViewModel? Vm => DataContext as LogViewModel;
    private void Level_Changed(object sender, SelectionChangedEventArgs e) { if (Vm != null && sender is ComboBox cb && cb.SelectedItem is ComboBoxItem item && item.Tag is string level) Vm.SetLevelCommand.Execute(level); }
}
