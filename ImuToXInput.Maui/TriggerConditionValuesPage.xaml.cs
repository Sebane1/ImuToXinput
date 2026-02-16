namespace ImuToXInput.Maui;

public partial class TriggerConditionValuesPage : ContentPage
{
    private readonly byte _initialWhenTrue;
    private readonly byte _initialWhenFalse;
    private readonly Action<byte, byte>? _onComplete;

    public TriggerConditionValuesPage(byte valueWhenTrue = 255, byte valueWhenFalse = 0, Action<byte, byte>? onComplete = null)
    {
        InitializeComponent();
        _initialWhenTrue = valueWhenTrue;
        _initialWhenFalse = valueWhenFalse;
        _onComplete = onComplete;
        EntryWhenTrue.Text = valueWhenTrue.ToString();
        EntryWhenFalse.Text = valueWhenFalse.ToString();
    }

    private async void OnOkClicked(object? sender, EventArgs e)
    {
        if (!byte.TryParse(EntryWhenTrue.Text, out var vT)) vT = _initialWhenTrue;
        if (!byte.TryParse(EntryWhenFalse.Text, out var vF)) vF = _initialWhenFalse;
        _onComplete?.Invoke(vT, vF);
        await Navigation.PopModalAsync();
    }

    private async void OnCancelClicked(object? sender, EventArgs e)
    {
        await Navigation.PopModalAsync();
    }
}
