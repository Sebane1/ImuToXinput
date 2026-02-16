using ImuToXInput.Config;
using Newtonsoft.Json;

namespace ImuToXInput.ConfigEditor;

public partial class MainForm : Form
{
    private string _configFolder = "";
    private string _configFolderDisplay = "";

    public MainForm()
    {
        InitializeComponent();
        var configsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "configs");
        SetConfigFolder(configsPath);
        RefreshList();
    }

    private void SetConfigFolder(string path)
    {
        _configFolder = path;
        _configFolderDisplay = string.IsNullOrEmpty(path) ? "(not set)" : path;
        lblFolder.Text = "Config folder: " + _configFolderDisplay;
    }

    private void RefreshList()
    {
        listConfigs.Items.Clear();
        if (string.IsNullOrEmpty(_configFolder) || !Directory.Exists(_configFolder))
        {
            return;
        }
        var files = Directory.GetFiles(_configFolder, "*.json", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .OrderBy(f => f?.ToLowerInvariant() == "default.json" ? "" : f)
            .ToList();
        foreach (var f in files)
            listConfigs.Items.Add(f ?? "");
    }

    private void btnBrowse_Click(object sender, EventArgs e)
    {
        using var dlg = new FolderBrowserDialog
        {
            Description = "Select the configs folder (contains game .json files)",
            SelectedPath = _configFolder
        };
        if (dlg.ShowDialog() == DialogResult.OK)
        {
            SetConfigFolder(dlg.SelectedPath);
            RefreshList();
        }
    }

    private void btnNew_Click(object sender, EventArgs e)
    {
        var profile = new GameProfile
        {
            Name = "New Game",
            ProcessNames = new List<string>(),
            AxisMappings = new List<AxisMapping>(),
            ButtonMappings = new List<ButtonMapping>(),
            TriggerMappings = new List<TriggerMapping>()
        };
        using var editor = new GameProfileEditorForm(profile, isNew: true, suggestedFileName: "mygame.json");
        if (editor.ShowDialog() != DialogResult.OK) return;
        var saved = editor.SavedProfile;
        if (saved == null) return;
        var fileName = editor.SavedFileName ?? "mygame.json";
        if (!fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) fileName += ".json";
        SaveProfile(Path.Combine(_configFolder, fileName), saved);
        RefreshList();
    }

    private void btnEdit_Click(object sender, EventArgs e)
    {
        var selected = listConfigs.SelectedItem?.ToString();
        if (string.IsNullOrEmpty(selected)) { MessageBox.Show("Select a config file first."); return; }
        var path = Path.Combine(_configFolder, selected);
        if (!File.Exists(path)) { MessageBox.Show("File not found."); return; }
        try
        {
            var json = File.ReadAllText(path);
            var profile = JsonConvert.DeserializeObject<GameProfile>(json);
            if (profile == null) { MessageBox.Show("Could not load config."); return; }
            using var editor = new GameProfileEditorForm(profile, isNew: false, suggestedFileName: selected);
            if (editor.ShowDialog() != DialogResult.OK) return;
            var saved = editor.SavedProfile;
            if (saved == null) return;
            SaveProfile(path, saved);
            RefreshList();
        }
        catch (Exception ex)
        {
            MessageBox.Show("Error loading config: " + ex.Message);
        }
    }

    private void SaveProfile(string path, GameProfile profile)
    {
        try
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            var json = JsonConvert.SerializeObject(profile, Formatting.Indented);
            File.WriteAllText(path, json);
            MessageBox.Show("Saved: " + Path.GetFileName(path));
        }
        catch (Exception ex)
        {
            MessageBox.Show("Error saving: " + ex.Message);
        }
    }

    private void btnDelete_Click(object sender, EventArgs e)
    {
        var selected = listConfigs.SelectedItem?.ToString();
        if (string.IsNullOrEmpty(selected)) { MessageBox.Show("Select a config file first."); return; }
        if (MessageBox.Show($"Delete {selected}?", "Delete config", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
        var path = Path.Combine(_configFolder, selected);
        try
        {
            if (File.Exists(path)) File.Delete(path);
            RefreshList();
        }
        catch (Exception ex) { MessageBox.Show("Error: " + ex.Message); }
    }

    private void btnOpenFolder_Click(object sender, EventArgs e)
    {
        if (string.IsNullOrEmpty(_configFolder) || !Directory.Exists(_configFolder))
        {
            MessageBox.Show("Config folder does not exist. Create it or browse to an existing folder.");
            return;
        }
        try { System.Diagnostics.Process.Start("explorer", _configFolder); }
        catch (Exception ex) { MessageBox.Show("Error: " + ex.Message); }
    }

    private void listConfigs_DoubleClick(object sender, EventArgs e) => btnEdit_Click(sender, e);

    private void listConfigs_MouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Right) return;
        var idx = listConfigs.IndexFromPoint(e.Location);
        if (idx < 0) return;
        var fileName = listConfigs.Items[idx]?.ToString();
        if (string.IsNullOrEmpty(fileName) || string.Equals(fileName, ConfigLoader.DefaultConfigFileName, StringComparison.OrdinalIgnoreCase))
        {
            return; // default.json uses DefaultProfile, not primary-per-game
        }
        var path = Path.Combine(_configFolder, fileName);
        if (!File.Exists(path)) return;
        try
        {
            var json = File.ReadAllText(path);
            var profile = JsonConvert.DeserializeObject<GameProfile>(json);
            if (profile?.ProcessNames == null || profile.ProcessNames.Count == 0) return;
            var menu = new ContextMenuStrip();
            foreach (var processName in profile.ProcessNames)
            {
                var p = processName;
                menu.Items.Add($"Set as primary for {p}", null, (_, _) =>
                {
                    PrimaryProfilesPreferences.SetPrimaryProfile(_configFolder, p, fileName);
                    MessageBox.Show($"\"{fileName}\" is now the primary profile for {p}.", "Primary profile");
                });
            }
            menu.Show(listConfigs, e.Location);
        }
        catch { /* ignore */ }
    }
}
