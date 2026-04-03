using Microsoft.Web.WebView2.Core;
using PhotoVault.ViewModels;

namespace PhotoVault.Views.Controls;

public partial class MapView : UserControl
{
    private bool _webViewReady;
    public MapView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        mapWebView.CoreWebView2InitializationCompleted += OnWebViewReady;
        InitWebView();
    }

    private async void InitWebView()
    {
        try
        {
            var env = await CoreWebView2Environment.CreateAsync();
            await mapWebView.EnsureCoreWebView2Async(env);
        }
        catch { }
    }

    private void OnWebViewReady(object? sender, CoreWebView2InitializationCompletedEventArgs e)
    {
        _webViewReady = e.IsSuccess;
        if (_webViewReady) LoadMap();
    }

    private void OnDataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
    {
        if (_webViewReady) LoadMap();
    }

    public void LoadMap()
    {
        if (!_webViewReady) return;
        if (DataContext is MapViewModel vm && !string.IsNullOrEmpty(vm.MapHtml))
        {
            mapWebView.NavigateToString(vm.MapHtml);
        }
    }
}
