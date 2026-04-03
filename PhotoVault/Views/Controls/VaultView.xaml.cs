using System.Windows;
using PhotoVault.ViewModels;

namespace PhotoVault.Views.Controls;

public partial class VaultView : UserControl
{
    public VaultView() { InitializeComponent(); }
    private VaultViewModel? Vm => DataContext as VaultViewModel;
    private void Unlock_Click(object sender, RoutedEventArgs e) { Vm?.UnlockCommand.Execute(pwdBox.Password); pwdBox.Clear(); }
    private void SetPwd_Click(object sender, RoutedEventArgs e) { Vm?.SetPasswordCommand.Execute(pwdBox.Password); pwdBox.Clear(); }
}
