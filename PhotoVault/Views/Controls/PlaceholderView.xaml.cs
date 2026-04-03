namespace PhotoVault.Views.Controls;

public partial class PlaceholderView : UserControl
{
    public PlaceholderView() { InitializeComponent(); }
    public void SetTitle(string title) { TitleText.Text = title; }
}
