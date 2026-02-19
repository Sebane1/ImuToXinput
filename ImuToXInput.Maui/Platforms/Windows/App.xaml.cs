using Microsoft.UI.Xaml;

namespace ImuToXInput.Maui.Platforms.Windows;

/// <summary>
/// WinUI application entry. Connects WinUI to the MAUI app via CreateMauiApp.
/// Uses official MAUI template: MauiWinUIApplication root in App.xaml generates Main.
/// </summary>
public partial class App : MauiWinUIApplication
{
    public App()
    {
        InitializeComponent();
    }

    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
