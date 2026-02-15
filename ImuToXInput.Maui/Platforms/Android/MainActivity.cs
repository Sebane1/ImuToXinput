using Android.App;
using Android.Content.PM;
using Android.OS;

namespace ImuToXInput.Maui;

[Activity(Theme = "@style/AppTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        // Apply edge-to-edge opt-out before window is created so content stays below status/title bars (Android 15+).
        if (Build.VERSION.SdkInt >= BuildVersionCodes.VanillaIceCream)
            Theme?.ApplyStyle(Resource.Style.OptOutEdgeToEdgeEnforcement, false);
        base.OnCreate(savedInstanceState);
    }
}
