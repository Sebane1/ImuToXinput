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
        AxisGrid.Children.Clear();
        AxisGrid.RowDefinitions.Clear();

        const int rowHeight = 24;
        var headerMargin = new Thickness(8, 0, 0, 0);

        // Row 0: header labels for all columns (so control row aligns the same in every column)
        AxisGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        AxisGrid.Add(new Label { Text = "Tracker", FontSize = 12, TextColor = Color.FromArgb("#888"), VerticalOptions = LayoutOptions.Center, Margin = headerMargin }, 0, 0);
        AxisGrid.Add(new Label { Text = "Source", FontSize = 12, TextColor = Color.FromArgb("#888"), VerticalOptions = LayoutOptions.Center, Margin = headerMargin }, 1, 0);
        AxisGrid.Add(new Label { Text = "Scale", FontSize = 12, TextColor = Color.FromArgb("#888"), VerticalOptions = LayoutOptions.Center, Margin = headerMargin }, 2, 0);
        AxisGrid.Add(new Label { Text = "Invert", FontSize = 12, TextColor = Color.FromArgb("#888"), VerticalOptions = LayoutOptions.Center }, 3, 0);
        AxisGrid.Add(new Label { Text = "Axis", FontSize = 12, TextColor = Color.FromArgb("#888"), VerticalOptions = LayoutOptions.Center, Margin = headerMargin }, 4, 0);
        AxisGrid.Add(new Label { Text = " ", FontSize = 12, TextColor = Color.FromArgb("#888"), VerticalOptions = LayoutOptions.Center }, 5, 0);

        int rowIndex = 1;
        foreach (var a in _profile.AxisMappings)
        {
            AxisGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Dropdowns (Picker) on Windows have internal top padding; push Scale/Invert/X down to align with dropdown content
            var controlTopMargin = new Thickness(0, 25, 0, 0);
            var trackerPicker = new Picker { Title = "Tracker", ItemsSource = ConfigEditorConstants.TrackerIds, MinimumHeightRequest = rowHeight, VerticalOptions = LayoutOptions.Center, HorizontalOptions = LayoutOptions.Fill };
            var sourcePicker = new Picker { Title = "Source", ItemsSource = ConfigEditorConstants.AxisSources, MinimumHeightRequest = rowHeight, VerticalOptions = LayoutOptions.Center, HorizontalOptions = LayoutOptions.Fill };
            var scaleEntry = new Entry { Text = a.Scale.ToString("G"), Keyboard = Keyboard.Numeric, Placeholder = "1", MinimumHeightRequest = rowHeight, VerticalOptions = LayoutOptions.Center, HorizontalOptions = LayoutOptions.Fill, Margin = controlTopMargin };
            var invertLabel = new Label { Text = "Invert", VerticalOptions = LayoutOptions.Center };
            var invertSwitch = new Switch { IsToggled = a.Invert, VerticalOptions = LayoutOptions.Center };
            var invertRow = new HorizontalStackLayout { Spacing = 4, VerticalOptions = LayoutOptions.Center, HorizontalOptions = LayoutOptions.Fill, Margin = controlTopMargin };
            invertRow.Children.Add(invertLabel);
            invertRow.Children.Add(invertSwitch);
            var axisPicker = new Picker { Title = "Axis", ItemsSource = ConfigEditorConstants.AxisOutputs, MinimumHeightRequest = rowHeight, VerticalOptions = LayoutOptions.Center, HorizontalOptions = LayoutOptions.Fill };
            var removeBtn = new Button { Text = "✕", WidthRequest = 32, MinimumHeightRequest = 22, MaximumHeightRequest = 22, Padding = new Thickness(10, 2), VerticalOptions = LayoutOptions.Center, HorizontalOptions = LayoutOptions.Center };
            var removeCell = new Grid { VerticalOptions = LayoutOptions.Fill, HorizontalOptions = LayoutOptions.Center, Margin = controlTopMargin };
            removeCell.Children.Add(removeBtn);

            trackerPicker.SelectedItem = a.Tracker;
            sourcePicker.SelectedItem = a.Source;
            axisPicker.SelectedItem = a.Axis;

            var binding = new AxisRowBinding { TrackerPicker = trackerPicker, SourcePicker = sourcePicker, ScaleEntry = scaleEntry, InvertSwitch = invertSwitch, AxisPicker = axisPicker, Mapping = a };
            trackerPicker.BindingContext = binding;

            removeBtn.Clicked += (_, _) =>
            {
                _profile.AxisMappings.Remove(a);
                RefreshAxisList();
            };

            AxisGrid.Add(trackerPicker, 0, rowIndex);
            AxisGrid.Add(sourcePicker, 1, rowIndex);
            AxisGrid.Add(scaleEntry, 2, rowIndex);
            AxisGrid.Add(invertRow, 3, rowIndex);
            AxisGrid.Add(axisPicker, 4, rowIndex);
            AxisGrid.Add(removeCell, 5, rowIndex);
            rowIndex++;
        }
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
        foreach (var child in AxisGrid.Children)
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
        ListTriggers.ItemsSource = _triggerMappings.Select(SummarizeTrigger).ToList();
    }

    private static string SummarizeTrigger(TriggerMapping m)
    {
        if (m.FixedValue.HasValue) return "= " + m.FixedValue.Value + " → " + m.Trigger;
        if (!string.IsNullOrEmpty(m.Tracker) && !string.IsNullOrEmpty(m.Source)) return m.Tracker + "." + m.Source + " → " + m.Trigger;
        var s = Summarize(m.Condition) + " → " + m.Trigger;
        if (m.ValueWhenTrue != 255 || m.ValueWhenFalse != 0)
            s += " = " + m.ValueWhenTrue + (m.ValueWhenFalse != 0 ? " / " + m.ValueWhenFalse : "");
        return s;
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

            var name = _profile.Name?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(name)) name = "config";
            var invalid = Path.GetInvalidFileNameChars();
            var fileName = string.Concat(name.Select(c => invalid.Contains(c) ? '_' : c));
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
        RefreshAxisList();
    }

    private void OnRemoveAxisClicked(object? sender, EventArgs e)
    {
        if (_profile.AxisMappings.Count > 0)
        {
            _profile.AxisMappings.RemoveAt(_profile.AxisMappings.Count - 1);
            RefreshAxisList();
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
        var choice = await DisplayActionSheet("Add trigger", "Cancel", null, "Condition (when …)", "Fixed value (0–255)", "Axis (tracker source)");
        if (string.IsNullOrEmpty(choice) || choice == "Cancel") return;
        var trigger = CmbTrigger.SelectedItem?.ToString() ?? "LeftTrigger";

        if (choice == "Fixed value (0–255)")
        {
            var raw = await DisplayPromptAsync("Fixed trigger value", "Value 0–255:", initialValue: "128", maxLength: 3, keyboard: Keyboard.Numeric);
            if (raw != null && byte.TryParse(raw, out byte val))
            {
                _triggerMappings.Add(new TriggerMapping { Trigger = trigger, FixedValue = val });
                RefreshTriggerList();
            }
            return;
        }
        if (choice == "Axis (tracker source)")
        {
            var tracker = await DisplayActionSheet("Tracker", "Cancel", null, ConfigEditorConstants.TrackerIds);
            if (string.IsNullOrEmpty(tracker) || tracker == "Cancel") return;
            var source = await DisplayActionSheet("Source", "Cancel", null, ConfigEditorConstants.AxisSources);
            if (string.IsNullOrEmpty(source) || source == "Cancel") return;
            _triggerMappings.Add(new TriggerMapping { Trigger = trigger, Tracker = tracker, Source = source, Scale = 1f, Invert = false });
            RefreshTriggerList();
            return;
        }

        MappingCondition? result = null;
        var page = new ConditionEditPage(null, c => result = c);
        await Navigation.PushModalAsync(page);
        if (result != null)
        {
            var condition = result;
            var valuesPage = new TriggerConditionValuesPage(255, 0, (t, f) =>
            {
                _triggerMappings.Add(new TriggerMapping { Condition = condition, Trigger = trigger, ValueWhenTrue = t, ValueWhenFalse = f });
                RefreshTriggerList();
            });
            await Navigation.PushModalAsync(valuesPage);
        }
    }

    private async void OnEditTriggerClicked(object? sender, EventArgs e)
    {
        var sel = ListTriggers.SelectedItem?.ToString();
        if (sel == null) return;
        var idx = _triggerMappings.FindIndex(m => SummarizeTrigger(m) == sel);
        if (idx < 0) return;
        var m = _triggerMappings[idx];
        if (m.FixedValue.HasValue || (!string.IsNullOrEmpty(m.Tracker) && !string.IsNullOrEmpty(m.Source)))
        {
            await DisplayAlert("Trigger", "Edit fixed value or axis triggers via the Script editor.", "OK");
            return;
        }
        MappingCondition? result = null;
        var page = new ConditionEditPage(m.Condition, c => result = c);
        await Navigation.PushModalAsync(page);
        if (result != null)
        {
            var condition = result;
            var triggerName = m.Trigger;
            var valuesPage = new TriggerConditionValuesPage(m.ValueWhenTrue, m.ValueWhenFalse, (t, f) =>
            {
                _triggerMappings[idx] = new TriggerMapping { Condition = condition, Trigger = triggerName, ValueWhenTrue = t, ValueWhenFalse = f };
                RefreshTriggerList();
            });
            await Navigation.PushModalAsync(valuesPage);
        }
    }

    private void OnRemoveTriggerClicked(object? sender, EventArgs e)
    {
        var sel = ListTriggers.SelectedItem?.ToString();
        if (sel == null) return;
        var idx = _triggerMappings.FindIndex(m => SummarizeTrigger(m) == sel);
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
