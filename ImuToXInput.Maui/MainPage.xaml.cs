using System.Diagnostics;
using ImuToXInput.Config;

namespace ImuToXInput.Maui;

public partial class MainPage : ContentPage
{
    private string _configFolder = "";
    private string _configFolderDisplay = "";
    private IDispatcherTimer? _profileInUseTimer;

    public const string ActiveProfilePreferenceKey = "ActiveProfileFileName";
    /// <summary>When set to "stepmania", mapping uses dance pad / StepMania mode instead of profile or FPS.</summary>
    public const string OverrideGameModePreferenceKey = "OverrideGameMode";
    /// <summary>When true, thumbsticks snap to neutral after hold and require return to deadzone (menu-friendly).</summary>
    public const string MenuModePreferenceKey = "ThumbstickMenuMode";

    public MainPage()
    {
        InitializeComponent();
        var configsPath = Path.Combine(FileSystem.AppDataDirectory, "configs");
        if (!Directory.Exists(configsPath))
        {
            Directory.CreateDirectory(configsPath);
        }
        SetConfigFolder(configsPath);
        RefreshList();
        BtnDongle.IsVisible = DeviceInfo.Platform == DevicePlatform.Android;
        BtnBrowse.IsVisible = DeviceInfo.Platform != DevicePlatform.Android;
        BorderActiveProfile.IsVisible = DeviceInfo.Platform == DevicePlatform.Android;
        BorderDancePad.IsVisible = DeviceInfo.Platform == DevicePlatform.Android;
        BorderMenuMode.IsVisible = DeviceInfo.Platform == DevicePlatform.Android;
        BorderProfileInUse.IsVisible = DeviceInfo.Platform == DevicePlatform.WinUI;
        RefreshActiveProfileLabel();
        RefreshDancePadSwitch();
        RefreshMenuModeSwitch();
        RefreshProfileInUseLabel();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        RefreshMenuModeSwitch(); // Update when returning (e.g. after gesture toggles menu mode)
        if (BorderProfileInUse.IsVisible)
        {
            _profileInUseTimer = Dispatcher.CreateTimer();
            _profileInUseTimer.Interval = TimeSpan.FromMilliseconds(500);
            _profileInUseTimer.Tick += (_, _) => RefreshProfileInUseLabel();
            _profileInUseTimer.Start();
            RefreshProfileInUseLabel();
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _profileInUseTimer?.Stop();
        _profileInUseTimer = null;
    }

    private void RefreshProfileInUseLabel()
    {
        if (!BorderProfileInUse.IsVisible || LblProfileInUse == null) return;
        if (!Services.ControllerLoopService.IsRunning)
        {
            LblProfileInUse.Text = "—";
            if (LblProfileInUseDetail != null)
            {
                LblProfileInUseDetail.Text = "Mapping not running.";
            }
            return;
        }
        if (Services.ControllerLoopService.CurrentIsStepMania)
        {
            LblProfileInUse.Text = "StepMania (dance pad)";
            if (LblProfileInUseDetail != null)
            {
                LblProfileInUseDetail.Text = "Feet map to pad arrows and corners.";
            }
            return;
        }
        var fileName = Services.ControllerLoopService.CurrentProfileFileName;
        if (string.IsNullOrEmpty(fileName))
        {
            LblProfileInUse.Text = "None";
            if (LblProfileInUseDetail != null)
            {
                LblProfileInUseDetail.Text = "No profile or default loaded.";
            }
            return;
        }
        LblProfileInUse.Text = fileName;
        if (LblProfileInUseDetail != null)
        {
            if (Services.ControllerLoopService.CurrentIsOverride)
            {
                LblProfileInUseDetail.Text = "Active profile (used when no game is detected).";
            }
            else if (!string.IsNullOrEmpty(Services.ControllerLoopService.CurrentProcessName))
            {
                LblProfileInUseDetail.Text = $"Auto-loaded for process: {Services.ControllerLoopService.CurrentProcessName}";
            }
            else
            {
                LblProfileInUseDetail.Text = "Default profile (no matching game running).";
            }
        }
    }

    private void RefreshActiveProfileLabel()
    {
        if (!BorderActiveProfile.IsVisible) return;
        var name = Preferences.Default.Get(ActiveProfilePreferenceKey, "");
        LblActiveProfile.Text = string.IsNullOrEmpty(name) ? "None" : name;
    }

    private void RefreshDancePadSwitch()
    {
        if (!BorderDancePad.IsVisible) return;
        SwitchDancePad.IsToggled = string.Equals(Preferences.Default.Get(OverrideGameModePreferenceKey, ""), "stepmania", StringComparison.OrdinalIgnoreCase);
    }

    private void OnDancePadToggled(object? sender, ToggledEventArgs e)
    {
        Preferences.Default.Set(OverrideGameModePreferenceKey, e.Value ? "stepmania" : "");
    }

    private void RefreshMenuModeSwitch()
    {
        if (!BorderMenuMode.IsVisible) return;
        SwitchMenuMode.IsToggled = Preferences.Default.Get(MenuModePreferenceKey, false);
    }

    private void OnMenuModeToggled(object? sender, ToggledEventArgs e)
    {
        Preferences.Default.Set(MenuModePreferenceKey, e.Value);
    }

    private async void OnSetActiveProfileClicked(object? sender, EventArgs e)
    {
        if (string.IsNullOrEmpty(_configFolder) || !Directory.Exists(_configFolder)) return;
        var files = Directory.GetFiles(_configFolder, "*.json", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .Where(f => !string.IsNullOrEmpty(f))
            .Cast<string>()
            .ToList();
        if (files.Count == 0)
        {
            await DisplayAlert("Active profile", "Create or download a profile first.", "OK");
            return;
        }
        var choice = await DisplayActionSheet("Use which profile for mapping?", "Cancel", null, files.ToArray());
        if (string.IsNullOrEmpty(choice) || choice == "Cancel") return;
        Preferences.Default.Set(ActiveProfilePreferenceKey, choice);
        RefreshActiveProfileLabel();
        await DisplayAlert("Active profile", $"Using \"{choice}\" for mapping.", "OK");
    }

    private async void OnDongleClicked(object? sender, EventArgs e)
    {
        await Navigation.PushAsync(new DongleConnectPage());
    }

    private void SetConfigFolder(string path)
    {
        _configFolder = path;
        _configFolderDisplay = string.IsNullOrEmpty(path) ? "(not set)" : path;
        LblFolder.Text = "Config folder: " + _configFolderDisplay;
    }

    private void OnProfileSaved()
    {
        Services.ControllerLoopService.InvalidateActiveProfileCache();
        Services.ControllerLoopService.ReloadConfig();
        RefreshList();
    }

    private void RefreshList()
    {
        ListConfigs.ItemsSource = null;
        if (string.IsNullOrEmpty(_configFolder) || !Directory.Exists(_configFolder))
        {
            return;
        }
        var files = Directory.GetFiles(_configFolder, "*.json", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .Where(f => !string.IsNullOrEmpty(f))
            .OrderBy(f => string.Equals(f, "default.json", StringComparison.OrdinalIgnoreCase) ? "" : f)
            .Cast<string>()
            .ToList();
        ListConfigs.ItemsSource = files;
        if (BorderActiveProfile.IsVisible)
        {
            RefreshActiveProfileLabel();
        }
    }

    private async void OnBrowseClicked(object? sender, EventArgs e)
    {
        if (string.IsNullOrEmpty(_configFolder) || !Directory.Exists(_configFolder))
        {
            await DisplayAlert("Config folder", "Config folder does not exist.", "OK");
            return;
        }
        try
        {
            Process.Start(new ProcessStartInfo { FileName = _configFolder, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", "Could not open folder: " + ex.Message, "OK");
        }
    }

    private void OnProfileSelected(object? sender, SelectionChangedEventArgs e) { }

    private async void OnNewClicked(object? sender, EventArgs e)
    {
        var profile = new GameProfile
        {
            Name = "New Game",
            ProcessNames = new List<string>(),
            AxisMappings = new List<AxisMapping>(),
            ButtonMappings = new List<ButtonMapping>(),
            TriggerMappings = new List<TriggerMapping>()
        };
        await Navigation.PushAsync(new ProfileEditorPage(profile, isNew: true, suggestedFileName: "mygame.json", _configFolder, OnProfileSaved));
    }

    private async void OnEditClicked(object? sender, EventArgs e)
    {
        var selected = ListConfigs.SelectedItem as string;
        if (string.IsNullOrEmpty(selected))
        {
            await DisplayAlert("Select a profile", "Select a config file from the list first.", "OK");
            return;
        }
        var path = Path.Combine(_configFolder, selected);
        if (!File.Exists(path))
        {
            await DisplayAlert("Not found", "File not found.", "OK");
            return;
        }
        try
        {
            var json = File.ReadAllText(path);
            var profile = Newtonsoft.Json.JsonConvert.DeserializeObject<GameProfile>(json);
            if (profile == null)
            {
                await DisplayAlert("Error", "Failed to parse config.", "OK");
                return;
            }
            await Navigation.PushAsync(new ProfileEditorPage(profile, isNew: false, suggestedFileName: selected, _configFolder, OnProfileSaved));
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", ex.Message, "OK");
        }
    }
}
