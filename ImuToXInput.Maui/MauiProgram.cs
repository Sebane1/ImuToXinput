namespace ImuToXInput.Maui;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                // Font file not in project; use platform default instead of OpenSans
            });

        return builder.Build();
    }
}
