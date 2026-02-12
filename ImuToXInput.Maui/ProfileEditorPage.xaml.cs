using System.Collections.ObjectModel;
using ImuToXInput.Config;

namespace ImuToXInput.Maui;

public partial class ProfileEditorPage : ContentPage
{
    private readonly GameProfile _profile;
    private readonly bool _isNew;
    private readonly string _suggestedFileName;
    private readonly string _configFolder;
    private readonly Action _onSaved;

    private readonly List<ButtonMapping> _buttonMappings = new();
    private readonly List<TriggerMapping> _triggerMappings = new();
    private readonly ObservableCollection<string> _processNames = new();
    private readonly ObservableCollection<string> _trackers = new();


    public ProfileEditorPage(GameProfile profile, bool isNew, string suggestedFileName, string configFolder, Action onSaved)
    {
        InitializeComponent();
        _profile = profile;
        _isNew = isNew;
        _suggestedFileName = suggestedFileName;
        _configFolder = configFolder;
        _onSaved = onSaved;

        CmbFloorTracker.ItemsSource = ConfigEditorConstants.FloorTrackerIds;
        CmbButton.ItemsSource = ConfigEditorConstants.Buttons;
        CmbTrigger.ItemsSource = ConfigEditorConstants.Triggers;

        ListProcessNames.ItemsSource = _processNames;
        ListFloorTrackers.ItemsSource = _trackers;

        LoadProfile();
    }

    private static string Summarize(MappingCondition? c)
    {
        if (c == null) return "(invalid)";
        if (c is EulerThresholdCondition et) return $"{et.Tracker}.{et.Component} {et.Op} {et.Value}";
        if (c is EulerDiffCondition ed) return $"{ed.TrackerA}-{ed.TrackerB}.{ed.Component} {ed.Op} {ed.Value}";
        if (c is EulerSumCondition es) return $"{es.TrackerA}+{es.TrackerB}.{es.Component} {es.Op} {es.Value}";
        if (c is PositionThresholdCondition pt) return $"{pt.Tracker}.{pt.Source} {pt.Op} {pt.Value}";
        return c.Type ?? "";
    }

    private void LoadProfile()
    {
        TxtName.Text = _profile.Name ?? "";
        _processNames.Clear();
        if (_profile.ProcessNames != null)
            foreach (var p in _profile.ProcessNames) _processNames.Add(p);
        _trackers.Clear();
        if (_profile.Trackers != null)
            foreach (var t in _profile.Trackers) _trackers.Add(t);

        _buttonMappings.Clear();
        if (_profile.ButtonMappings != null)
            _buttonMappings.AddRange(_profile.ButtonMappings);
        _triggerMappings.Clear();
        if (_profile.TriggerMappings != null)
            _triggerMappings.AddRange(_profile.TriggerMappings);

        RefreshAxisList();
        RefreshButtonList();
        RefreshTriggerList();
        UpdateScriptFromProfile(fromForm: false);
    }

    private void RefreshAxisList()
    {
        AxisList.Children.Clear();
        foreach (var a in _profile.AxisMappings)
            AddAxisRow(a);
    }

    private void AddAxisRow(AxisMapping a)
    {
        var trackerPicker = new Picker { Title = "Tracker", ItemsSource = ConfigEditorConstants.TrackerIds, WidthRequest = 100 };
        var sourcePicker = new Picker { Title = "Source", ItemsSource = ConfigEditorConstants.AxisSources, WidthRequest = 90 };
        var scaleEntry = new Entry { Text = a.Scale.ToString("G"), Keyboard = Keyboard.Numeric, WidthRequest = 50 };
        var invertSwitch = new Switch { IsToggled = a.Invert };
        var axisPicker = new Picker { Title = "Axis", ItemsSource = ConfigEditorConstants.AxisOutputs, WidthRequest = 100 };
        var removeBtn = new Button { Text = "✕", WidthRequest = 36 };

        trackerPicker.SelectedItem = a.Tracker;
        sourcePicker.SelectedItem = a.Source;
        axisPicker.SelectedItem = a.Axis;

        var row = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection
            {
                new() { Width = new GridLength(100) },
                new() { Width = new GridLength(90) },
                new() { Width = new GridLength(50) },
                new() { Width = new GridLength(50) },
                new() { Width = new GridLength(100) },
                new() { Width = new GridLength(40) }
            },
            Padding = new Thickness(0, 4),
            ColumnSpacing = 4
        };
        row.Add(trackerPicker, 0); row.Add(sourcePicker, 1); row.Add(scaleEntry, 2);
        row.Add(invertSwitch, 3); row.Add(axisPicker, 4); row.Add(removeBtn, 5);
        row.BindingContext = new AxisRowBinding { TrackerPicker = trackerPicker, SourcePicker = sourcePicker, ScaleEntry = scaleEntry, InvertSwitch = invertSwitch, AxisPicker = axisPicker, Mapping = a };

        removeBtn.Clicked += (_, _) =>
        {
            _profile.AxisMappings.Remove(a);
            AxisList.Children.Remove(row);
        };

        AxisList.Children.Add(row);
    }

    private sealed class AxisRowBinding
    {
        public Picker TrackerPicker { get; init; } = null!;
        public Picker SourcePicker { get; init; } = null!;
        public Entry ScaleEntry { get; init; } = null!;
        public Switch InvertSwitch { get; init; } = null!;
        public Picker AxisPicker { get; init; } = null!;
        public AxisMapping Mapping { get; init; } = null!;
    }

    private void SaveFormToProfile()
    {
        _profile.Name = TxtName.Text?.Trim() ?? "";
        _profile.ProcessNames = _processNames.Count > 0 ? _processNames.ToList() : null;
        _profile.Trackers = _trackers.Count > 0 ? _trackers.ToList() : null;

        _profile.AxisMappings.Clear();
        foreach (var child in AxisList.Children)
        {
            if (child is not View v || v.BindingContext is not AxisRowBinding binding) continue;
            float scale = float.TryParse(binding.ScaleEntry.Text, out var s) ? s : 1f;
            _profile.AxisMappings.Add(new AxisMapping
            {
                Tracker = binding.TrackerPicker.SelectedItem?.ToString() ?? "",
                Source = binding.SourcePicker.SelectedItem?.ToString() ?? "",
                Scale = scale,
                Invert = binding.InvertSwitch.IsToggled,
                Axis = binding.AxisPicker.SelectedItem?.ToString() ?? ""
            });
        }

        _profile.ButtonMappings.Clear();
        _profile.ButtonMappings.AddRange(_buttonMappings);
        _profile.TriggerMappings.Clear();
        _profile.TriggerMappings.AddRange(_triggerMappings);
    }

    private void RefreshButtonList()
    {
        ListButtons.ItemsSource = _buttonMappings
            .Select(m => Summarize(m.Condition) + " → " + m.Button)
            .ToList();
    }

    private void RefreshTriggerList()
    {
        ListTriggers.ItemsSource = _triggerMappings
            .Select(m => Summarize(m.Condition) + " → " + m.Trigger)
            .ToList();
    }

    private void UpdateScriptFromProfile(bool fromForm)
    {
        if (fromForm) SaveFormToProfile();
        var script = ScriptFormat.FormatProfile(_profile);
        ScriptWebView.Source = new HtmlWebViewSource { Html = ScriptEditorWebView.BuildHtml(script) };
    }

    private async Task<string?> GetScriptContentAsync()
    {
        try
        {
            var result = await ScriptWebView.EvaluateJavaScriptAsync(ScriptEditorWebView.GetContentScript);
            if (string.IsNullOrEmpty(result)) return null;
            // Result is base64 from the WebView (avoids bridge mangling newlines/quotes)
            result = result.Trim();
            if (result.StartsWith('"') && result.EndsWith('"'))
                result = Newtonsoft.Json.JsonConvert.DeserializeObject<string>(result) ?? result;
            try
            {
                var bytes = Convert.FromBase64String(result);
                return System.Text.Encoding.UTF8.GetString(bytes);
            }
            catch
            {
                return null;
            }
        }
        catch
        {
            return null;
        }
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        try
        {
            var content = await GetScriptContentAsync();
            if (content != null)
            {
                var parsed = ScriptFormat.ParseProfile(content);
                _profile.Name = TxtName.Text?.Trim() ?? parsed.Name;
                _profile.ProcessNames = _processNames.Count > 0 ? _processNames.ToList() : parsed.ProcessNames;
                _profile.Trackers = _trackers.Count > 0 ? _trackers.ToList() : parsed.Trackers;
                _profile.AxisMappings = parsed.AxisMappings;
                _profile.ButtonMappings = parsed.ButtonMappings;
                _profile.TriggerMappings = parsed.TriggerMappings;
            }
            else
                SaveFormToProfile();

            var fileName = _suggestedFileName;
            if (!fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                fileName += ".json";
            var path = Path.Combine(_configFolder, fileName);
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(_profile, Newtonsoft.Json.Formatting.Indented);
            await File.WriteAllTextAsync(path, json);
            _onSaved();
            await Navigation.PopAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", ex.Message, "OK");
        }
    }

    private async void OnCancelClicked(object? sender, EventArgs e) => await Navigation.PopAsync();

    private void OnAddProcessNameClicked(object? sender, EventArgs e)
    {
        var s = TxtProcessName.Text?.Trim();
        if (string.IsNullOrEmpty(s)) return;
        _processNames.Add(s);
        TxtProcessName.Text = "";
    }

    private void OnRemoveProcessNameClicked(object? sender, EventArgs e)
    {
        if (ListProcessNames.SelectedItem is string s)
            _processNames.Remove(s);
    }

    private void OnAddFloorTrackerClicked(object? sender, EventArgs e)
    {
        if (CmbFloorTracker.SelectedItem is string t)
            _trackers.Add(t);
    }

    private void OnRemoveFloorTrackerClicked(object? sender, EventArgs e)
    {
        if (ListFloorTrackers.SelectedItem is string s)
            _trackers.Remove(s);
    }

    private void OnAddAxisClicked(object? sender, EventArgs e)
    {
        var a = new AxisMapping { Tracker = "HEAD", Source = "EulerX", Scale = 1f, Invert = false, Axis = "RightThumbX" };
        _profile.AxisMappings.Add(a);
        AddAxisRow(a);
    }

    private void OnRemoveAxisClicked(object? sender, EventArgs e)
    {
        // Remove last row for simplicity (or track selection)
        if (AxisList.Children.Count > 0 && AxisList.Children[^1] is View last && last.BindingContext is AxisRowBinding binding)
        {
            _profile.AxisMappings.Remove(binding.Mapping);
            AxisList.Children.RemoveAt(AxisList.Children.Count - 1);
        }
    }

    private async void OnAddButtonClicked(object? sender, EventArgs e)
    {
        MappingCondition? result = null;
        var page = new ConditionEditPage(null, c => result = c);
        await Navigation.PushModalAsync(page);
        if (result != null)
        {
            var button = CmbButton.SelectedItem?.ToString() ?? "A";
            _buttonMappings.Add(new ButtonMapping { Condition = result, Button = button });
            RefreshButtonList();
        }
    }

    private async void OnEditButtonClicked(object? sender, EventArgs e)
    {
        var idx = ListButtons.SelectedItem != null ? _buttonMappings.IndexOf(_buttonMappings.First(m => (Summarize(m.Condition) + " → " + m.Button) == ListButtons.SelectedItem.ToString())) : -1;
        if (idx < 0 || idx >= _buttonMappings.Count) return;
        var m = _buttonMappings[idx];
        MappingCondition? result = null;
        var page = new ConditionEditPage(m.Condition, c => result = c);
        await Navigation.PushModalAsync(page);
        if (result != null)
        {
            _buttonMappings[idx] = new ButtonMapping { Condition = result, Button = m.Button };
            RefreshButtonList();
        }
    }

    private void OnRemoveButtonClicked(object? sender, EventArgs e)
    {
        var sel = ListButtons.SelectedItem?.ToString();
        if (sel == null) return;
        var idx = _buttonMappings.FindIndex(m => (Summarize(m.Condition) + " → " + m.Button) == sel);
        if (idx >= 0) { _buttonMappings.RemoveAt(idx); RefreshButtonList(); }
    }

    private async void OnAddTriggerClicked(object? sender, EventArgs e)
    {
        MappingCondition? result = null;
        var page = new ConditionEditPage(null, c => result = c);
        await Navigation.PushModalAsync(page);
        if (result != null)
        {
            var trigger = CmbTrigger.SelectedItem?.ToString() ?? "LeftTrigger";
            _triggerMappings.Add(new TriggerMapping { Condition = result, Trigger = trigger, ValueWhenTrue = 255, ValueWhenFalse = 0 });
            RefreshTriggerList();
        }
    }

    private async void OnEditTriggerClicked(object? sender, EventArgs e)
    {
        var sel = ListTriggers.SelectedItem?.ToString();
        if (sel == null) return;
        var idx = _triggerMappings.FindIndex(m => (Summarize(m.Condition) + " → " + m.Trigger) == sel);
        if (idx < 0) return;
        var m = _triggerMappings[idx];
        MappingCondition? result = null;
        var page = new ConditionEditPage(m.Condition, c => result = c);
        await Navigation.PushModalAsync(page);
        if (result != null)
        {
            _triggerMappings[idx] = new TriggerMapping { Condition = result, Trigger = m.Trigger, ValueWhenTrue = m.ValueWhenTrue, ValueWhenFalse = m.ValueWhenFalse };
            RefreshTriggerList();
        }
    }

    private void OnRemoveTriggerClicked(object? sender, EventArgs e)
    {
        var sel = ListTriggers.SelectedItem?.ToString();
        if (sel == null) return;
        var idx = _triggerMappings.FindIndex(m => (Summarize(m.Condition) + " → " + m.Trigger) == sel);
        if (idx >= 0) { _triggerMappings.RemoveAt(idx); RefreshTriggerList(); }
    }

    private void OnRefreshFromFormClicked(object? sender, EventArgs e) => UpdateScriptFromProfile(fromForm: true);

    private async void OnApplyScriptClicked(object? sender, EventArgs e)
    {
        try
        {
            var content = await GetScriptContentAsync();
            if (content == null) { await DisplayAlert("Apply script", "Could not read script from editor.", "OK"); return; }
            var parsed = ScriptFormat.ParseProfile(content);
            _profile.Name = parsed.Name;
            _profile.ProcessNames = parsed.ProcessNames ?? new List<string>();
            _profile.Trackers = parsed.Trackers;
            _profile.AxisMappings = parsed.AxisMappings;
            _profile.ButtonMappings = parsed.ButtonMappings;
            _profile.TriggerMappings = parsed.TriggerMappings;
            LoadProfile();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Apply script", "Script parse error: " + ex.Message, "OK");
        }
    }
}
