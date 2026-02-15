using ImuToXInput.Maui.Services;

namespace ImuToXInput.Maui;

public partial class SharedConfigsPage : ContentPage
{
    private readonly SharedConfigService _service = new();
    private string ConfigFolder => Path.Combine(FileSystem.AppDataDirectory, "configs");

    public SharedConfigsPage()
    {
        InitializeComponent();
        // Point to your deployed server. For Android emulator use http://10.0.2.2:5000 (or your host port).
        _service.BaseUrl = "https://localhost:7001";
    }

    private async void OnSearchPressed(object? sender, EventArgs e)
    {
        LblEmpty.IsVisible = false;
        ListResults.IsVisible = false;
        Loading.IsVisible = true;
        Loading.IsRunning = true;
        try
        {
            var search = SearchBar.Text?.Trim();
            var list = await _service.SearchAsync(search);
            ListResults.ItemsSource = list;
            ListResults.IsVisible = true;
            if (list.Count == 0)
            {
                LblEmpty.Text = "No configs found. Try a different search or upload one.";
                LblEmpty.IsVisible = true;
            }
            else
                LblEmpty.IsVisible = false;
        }
        catch (Exception ex)
        {
            LblEmpty.Text = "Error: " + ex.Message;
            LblEmpty.IsVisible = true;
        }
        finally
        {
            Loading.IsVisible = false;
            Loading.IsRunning = false;
        }
    }

    private async void OnDownloadClicked(object? sender, EventArgs e)
    {
        if (sender is not Button btn || btn.CommandParameter is not string id) return;
        try
        {
            var full = await _service.GetByIdAsync(id);
            if (full == null || string.IsNullOrWhiteSpace(full.JsonContent))
            {
                await DisplayAlert("Download", "Config not found or empty.", "OK");
                return;
            }
            if (!Directory.Exists(ConfigFolder))
                Directory.CreateDirectory(ConfigFolder);
            var safeName = string.Join("_", full.GameName.Split(Path.GetInvalidFileNameChars()));
            var fileName = $"{safeName}_{full.ConfigName}.json".Replace(" ", "_");
            if (!fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                fileName += ".json";
            var path = Path.Combine(ConfigFolder, fileName);
            await File.WriteAllTextAsync(path, full.JsonContent);
            await DisplayAlert("Download", $"Saved as {fileName}", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", ex.Message, "OK");
        }
    }

    private async void OnUploadClicked(object? sender, EventArgs e)
    {
        if (!Directory.Exists(ConfigFolder))
        {
            await DisplayAlert("Upload", "No config folder. Create a profile first.", "OK");
            return;
        }
        var files = Directory.GetFiles(ConfigFolder, "*.json", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .Where(f => !string.IsNullOrEmpty(f))
            .Cast<string>()
            .ToList();
        if (files.Count == 0)
        {
            await DisplayAlert("Upload", "No profiles to upload. Create one first.", "OK");
            return;
        }
        var choice = await DisplayActionSheet("Choose profile to share", "Cancel", null, files.ToArray());
        if (string.IsNullOrEmpty(choice) || choice == "Cancel") return;
        try
        {
            var path = Path.Combine(ConfigFolder, choice);
            var json = await File.ReadAllTextAsync(path);
            var profile = Newtonsoft.Json.JsonConvert.DeserializeObject<ImuToXInput.Config.GameProfile>(json);
            var gameName = profile?.Name ?? Path.GetFileNameWithoutExtension(choice);
            var configName = Path.GetFileNameWithoutExtension(choice);
            var result = await _service.UploadAsync(gameName, configName, json);
            if (result != null)
                await DisplayAlert("Upload", $"Shared as {result.GameName} / {result.ConfigName}.", "OK");
            else
                await DisplayAlert("Upload", "Upload failed. Check server URL and connection.", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", ex.Message, "OK");
        }
    }
}
