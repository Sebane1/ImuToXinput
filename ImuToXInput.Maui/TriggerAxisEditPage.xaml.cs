using ImuToXInput.Config;
using ImuToXInput.Core;

namespace ImuToXInput.Maui;

public partial class TriggerAxisEditPage : ContentPage
{
    private readonly Action<string, string, float, bool>? _onComplete;

    public TriggerAxisEditPage(string tracker, string source, float scale, bool invert, Action<string, string, float, bool>? onComplete = null)
    {
        InitializeComponent();
        _onComplete = onComplete;

        PickerTracker.ItemsSource = ConfigEditorConstants.TrackerIds.ToList();
        PickerSource.ItemsSource = ConfigEditorConstants.AxisSources.ToList();

        var trackerIdx = Array.FindIndex(ConfigEditorConstants.TrackerIds, t => string.Equals(t, tracker, StringComparison.OrdinalIgnoreCase));
        if (trackerIdx >= 0) PickerTracker.SelectedIndex = trackerIdx;

        var sourceIdx = Array.FindIndex(ConfigEditorConstants.AxisSources, s => string.Equals(s, source, StringComparison.OrdinalIgnoreCase));
        if (sourceIdx >= 0) PickerSource.SelectedIndex = sourceIdx;

        EntryScale.Text = scale.ToString(System.Globalization.CultureInfo.InvariantCulture);
        SwitchInvert.IsToggled = invert;
    }

    private async void OnOkClicked(object? sender, EventArgs e)
    {
        var tracker = PickerTracker.SelectedItem?.ToString() ?? "";
        var source = PickerSource.SelectedItem?.ToString() ?? "";
        if (string.IsNullOrEmpty(tracker) || string.IsNullOrEmpty(source))
        {
            await DisplayAlertAsync("Error", "Select both tracker and source.", "OK");
            return;
        }
        var scale = float.TryParse(EntryScale.Text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var s) ? s : 1f;
        _onComplete?.Invoke(tracker, source, scale, SwitchInvert.IsToggled);
        await Navigation.PopModalAsync();
    }

    private async void OnCancelClicked(object? sender, EventArgs e)
    {
        await Navigation.PopModalAsync();
    }
}
