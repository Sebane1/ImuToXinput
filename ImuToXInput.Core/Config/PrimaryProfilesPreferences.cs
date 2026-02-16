using Newtonsoft.Json;

namespace ImuToXInput.Config;

/// <summary>
/// Maps process names to primary profile file names. When multiple profiles target the same game,
/// the primary profile for that game is used. Stored as primaryProfiles.json in the config folder.
/// </summary>
public static class PrimaryProfilesPreferences
{
    public const string FileName = "primaryProfiles.json";

    /// <summary>Get the primary profile file name for a process, or null if not set.</summary>
    public static string? GetPrimaryProfile(string configDirectory, string processName)
    {
        var path = Path.Combine(configDirectory, FileName);
        if (!File.Exists(path)) return null;
        try
        {
            var json = File.ReadAllText(path);
            var map = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
            if (map == null) return null;
            var key = processName;
            return map.TryGetValue(key, out var fileName) ? fileName : null;
        }
        catch { return null; }
    }

    /// <summary>Set the primary profile file name for a process.</summary>
    public static void SetPrimaryProfile(string configDirectory, string processName, string profileFileName)
    {
        var path = Path.Combine(configDirectory, FileName);
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (File.Exists(path))
        {
            try
            {
                var json = File.ReadAllText(path);
                var existing = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                if (existing != null)
                {
                    foreach (var kv in existing)
                        map[kv.Key] = kv.Value;
                }
            }
            catch { /* start fresh */ }
        }
        map[processName] = profileFileName;
        Directory.CreateDirectory(configDirectory);
        File.WriteAllText(path, JsonConvert.SerializeObject(map, Formatting.Indented));
    }

    /// <summary>Remove the primary profile for a process.</summary>
    public static void ClearPrimaryProfile(string configDirectory, string processName)
    {
        var path = Path.Combine(configDirectory, FileName);
        if (!File.Exists(path)) return;
        try
        {
            var json = File.ReadAllText(path);
            var map = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
            if (map == null) return;
            map.Remove(processName);
            if (map.Count == 0)
            {
                File.Delete(path);
                return;
            }
            File.WriteAllText(path, JsonConvert.SerializeObject(map, Formatting.Indented));
        }
        catch { /* ignore */ }
    }

    /// <summary>Load all primary profile mappings (process -> fileName).</summary>
    public static Dictionary<string, string> LoadAll(string configDirectory)
    {
        var path = Path.Combine(configDirectory, FileName);
        if (!File.Exists(path)) return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var json = File.ReadAllText(path);
            var map = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
            return map ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }
}
