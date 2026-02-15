using System.Diagnostics;
using ImuToXInput.Config;

namespace ImuToXInput.Maui;

public partial class MainPage : ContentPage
{
    private string _configFolder = "";
    private string _configFolderDisplay = "";

    public MainPage()
    {
        InitializeComponent();
        var configsPath = Path.Combine(FileSystem.AppDataDirectory, "configs");
        if (!Directory.Exists(configsPath))
            Directory.CreateDirectory(configsPath);
        SetConfigFolder(configsPath);
        RefreshList();
        BtnDongle.IsVisible = DeviceInfo.Platform == DevicePlatform.Android;
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

    private void RefreshList()
    {
        ListConfigs.ItemsSource = null;
        if (string.IsNullOrEmpty(_configFolder) || !Directory.Exists(_configFolder))
            return;
        var files = Directory.GetFiles(_configFolder, "*.json", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .Where(f => !string.IsNullOrEmpty(f))
            .OrderBy(f => string.Equals(f, "default.json", StringComparison.OrdinalIgnoreCase) ? "" : f)
            .Cast<string>()
            .ToList();
        ListConfigs.ItemsSource = files;
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
        await Navigation.PushAsync(new ProfileEditorPage(profile, isNew: true, suggestedFileName: "mygame.json", _configFolder, RefreshList));
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
            await Navigation.PushAsync(new ProfileEditorPage(profile, isNew: false, suggestedFileName: selected, _configFolder, RefreshList));
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", ex.Message, "OK");
        }
    }
}
