namespace ImuToXInput.Maui;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            LogException("UnhandledException", (Exception)e.ExceptionObject);
#if DEBUG
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            LogException("UnobservedTaskException", e.Exception);
            e.SetObserved();
        };
#endif
    }

    private static void LogException(string tag, Exception ex)
    {
        System.Diagnostics.Debug.WriteLine($"[{tag}] {ex?.GetType().FullName}: {ex?.Message}");
        System.Diagnostics.Debug.WriteLine(ex?.ToString());
        for (var inner = ex?.InnerException; inner != null; inner = inner.InnerException)
            System.Diagnostics.Debug.WriteLine($"  Inner: {inner.GetType().Name}: {inner.Message}");
    }

    protected override Window CreateWindow(IActivationState activationState)
    {
        // Use Shell: its Android implementation may attach content where bare ContentPage does not.
        return new Window(new AppShell());
    }
}
