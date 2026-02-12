using ImuToXInput.Config;

namespace ImuToXInput.Maui;

public partial class ConditionEditPage : ContentPage
{
    private readonly MappingCondition? _existing;
    private readonly Action<MappingCondition?> _onComplete;

    private Picker? _pickerTracker;
    private Picker? _pickerTrackerA;
    private Picker? _pickerTrackerB;
    private Picker? _pickerComponent;
    private Picker? _pickerSource;
    private Picker? _pickerOp;
    private Entry? _entryValue;

    public ConditionEditPage(MappingCondition? existing, Action<MappingCondition?> onComplete)
    {
        InitializeComponent();
        _existing = existing;
        _onComplete = onComplete;

        PickerType.ItemsSource = ConfigEditorConstants.ConditionTypes;
        PickerType.SelectedIndexChanged += (_, _) => RefreshConditionFields();

        if (_existing != null)
            PickerType.SelectedItem = _existing.Type;
        else
            PickerType.SelectedIndex = 0;

        RefreshConditionFields();
    }

    private void LoadCondition(MappingCondition c)
    {
        if (c is EulerThresholdCondition et)
        {
            PickerType.SelectedItem = "euler_threshold";
            _pickerTracker!.SelectedItem = et.Tracker;
            _pickerComponent!.SelectedItem = et.Component;
            _pickerOp!.SelectedItem = et.Op;
            _entryValue!.Text = et.Value.ToString("G");
        }
        else if (c is EulerDiffCondition ed)
        {
            PickerType.SelectedItem = "euler_diff";
            _pickerTrackerA!.SelectedItem = ed.TrackerA;
            _pickerTrackerB!.SelectedItem = ed.TrackerB;
            _pickerComponent!.SelectedItem = ed.Component;
            _pickerOp!.SelectedItem = ed.Op;
            _entryValue!.Text = ed.Value.ToString("G");
        }
        else if (c is EulerSumCondition es)
        {
            PickerType.SelectedItem = "euler_sum";
            _pickerTrackerA!.SelectedItem = es.TrackerA;
            _pickerTrackerB!.SelectedItem = es.TrackerB;
            _pickerComponent!.SelectedItem = es.Component;
            _pickerOp!.SelectedItem = es.Op;
            _entryValue!.Text = es.Value.ToString("G");
        }
        else if (c is PositionThresholdCondition pt)
        {
            PickerType.SelectedItem = "position_threshold";
            _pickerTracker!.SelectedItem = pt.Tracker;
            _pickerSource!.SelectedItem = pt.Source;
            _pickerOp!.SelectedItem = pt.Op;
            _entryValue!.Text = pt.Value.ToString("G");
        }
    }

    private void RefreshConditionFields()
    {
        PanelFields.Children.Clear();
        _pickerTracker = null;
        _pickerTrackerA = null;
        _pickerTrackerB = null;
        _pickerComponent = null;
        _pickerSource = null;
        _pickerOp = null;
        _entryValue = null;

        var type = PickerType.SelectedItem?.ToString() ?? "euler_threshold";

        void AddRow(string label, View control)
        {
            PanelFields.Children.Add(new Label { Text = label });
            PanelFields.Children.Add(control);
        }

        _pickerOp = new Picker { Title = "Operator", ItemsSource = ConfigEditorConstants.Operators };
        _entryValue = new Entry { Placeholder = "Value", Keyboard = Keyboard.Numeric };

        if (type == "euler_threshold")
        {
            _pickerTracker = new Picker { Title = "Tracker", ItemsSource = ConfigEditorConstants.TrackerIds };
            _pickerComponent = new Picker { Title = "Component", ItemsSource = ConfigEditorConstants.EulerComponents };
            AddRow("Tracker:", _pickerTracker);
            AddRow("Component (X/Y/Z):", _pickerComponent);
            AddRow("Operator:", _pickerOp);
            AddRow("Value:", _entryValue);
        }
        else if (type == "euler_diff" || type == "euler_sum")
        {
            _pickerTrackerA = new Picker { Title = "Tracker A", ItemsSource = ConfigEditorConstants.TrackerIds };
            _pickerTrackerB = new Picker { Title = "Tracker B", ItemsSource = ConfigEditorConstants.TrackerIds };
            _pickerComponent = new Picker { Title = "Component", ItemsSource = ConfigEditorConstants.EulerComponents };
            AddRow("Tracker A:", _pickerTrackerA);
            AddRow("Tracker B:", _pickerTrackerB);
            AddRow("Component:", _pickerComponent);
            AddRow("Operator:", _pickerOp);
            AddRow("Value:", _entryValue);
        }
        else
        {
            _pickerTracker = new Picker { Title = "Tracker", ItemsSource = ConfigEditorConstants.TrackerIds };
            _pickerSource = new Picker { Title = "Source", ItemsSource = ConfigEditorConstants.PositionSources };
            AddRow("Tracker:", _pickerTracker);
            AddRow("Source:", _pickerSource);
            AddRow("Operator:", _pickerOp);
            AddRow("Value:", _entryValue);
        }

        if (_existing != null)
            LoadCondition(_existing);
    }

    private async void OnOkClicked(object? sender, EventArgs e)
    {
        var c = BuildCondition();
        if (c == null)
        {
            await DisplayAlert("Error", "Invalid condition.", "OK");
            return;
        }
        _onComplete(c);
        await Navigation.PopModalAsync();
    }

    private async void OnCancelClicked(object? sender, EventArgs e)
    {
        _onComplete(null);
        await Navigation.PopModalAsync();
    }

    private MappingCondition? BuildCondition()
    {
        var type = PickerType.SelectedItem?.ToString() ?? "";
        var op = _pickerOp?.SelectedItem?.ToString() ?? "greater_than";
        if (!float.TryParse(_entryValue?.Text, out var value))
            value = 0f;

        return type switch
        {
            "euler_threshold" => new EulerThresholdCondition
            {
                Type = type,
                Tracker = _pickerTracker?.SelectedItem?.ToString() ?? "",
                Component = _pickerComponent?.SelectedItem?.ToString() ?? "X",
                Op = op,
                Value = value
            },
            "euler_diff" => new EulerDiffCondition
            {
                Type = type,
                TrackerA = _pickerTrackerA?.SelectedItem?.ToString() ?? "",
                TrackerB = _pickerTrackerB?.SelectedItem?.ToString() ?? "",
                Component = _pickerComponent?.SelectedItem?.ToString() ?? "X",
                Op = op,
                Value = value
            },
            "euler_sum" => new EulerSumCondition
            {
                Type = type,
                TrackerA = _pickerTrackerA?.SelectedItem?.ToString() ?? "",
                TrackerB = _pickerTrackerB?.SelectedItem?.ToString() ?? "",
                Component = _pickerComponent?.SelectedItem?.ToString() ?? "X",
                Op = op,
                Value = value
            },
            "position_threshold" => new PositionThresholdCondition
            {
                Type = type,
                Tracker = _pickerTracker?.SelectedItem?.ToString() ?? "",
                Source = _pickerSource?.SelectedItem?.ToString() ?? "FloorRelY",
                Op = op,
                Value = value
            },
            _ => null
        };
    }
}
