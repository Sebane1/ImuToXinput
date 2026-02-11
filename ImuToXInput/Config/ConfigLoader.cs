using Newtonsoft.Json;
using System.Linq;

namespace ImuToXInput.Config
{
    /// <summary>
    /// Result of loading the configs folder: one profile per game file plus the default profile.
    /// </summary>
    public class LoadedConfig
    {
        public List<GameProfile> Profiles { get; set; } = new();
        public GameProfile? DefaultProfile { get; set; }
    }

    public static class ConfigLoader
    {
        /// <summary>
        /// Folder name next to the executable. Each .json file = one game profile.
        /// </summary>
        public const string ConfigFolderName = "configs";

        /// <summary>
        /// Filename for the profile used when no game process matches (e.g. generic FPS).
        /// </summary>
        public const string DefaultConfigFileName = "default.json";

        public static string GetConfigDirectory()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ConfigFolderName);
        }

        /// <summary>
        /// Load all .json files from the configs folder. default.json is used as the fallback profile.
        /// </summary>
        public static LoadedConfig? Load()
        {
            var dir = GetConfigDirectory();
            if (!Directory.Exists(dir))
                return null;

            var result = new LoadedConfig();
            var files = Directory.GetFiles(dir, "*.json", SearchOption.TopDirectoryOnly);

            foreach (var path in files)
            {
                try
                {
                    var json = File.ReadAllText(path);
                    var profile = JsonConvert.DeserializeObject<GameProfile>(json);
                    if (profile == null) continue;

                    var fileName = Path.GetFileName(path);
                    if (string.Equals(fileName, DefaultConfigFileName, StringComparison.OrdinalIgnoreCase))
                        result.DefaultProfile = profile;
                    else
                        result.Profiles.Add(profile);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to load config {Path.GetFileName(path)}: {ex.Message}");
                }
            }

            if (result.DefaultProfile == null && result.Profiles.Count > 0)
                result.DefaultProfile = result.Profiles.FirstOrDefault(p =>
                    string.Equals(p.Name, "default", StringComparison.OrdinalIgnoreCase)) ?? result.Profiles[0];

            return result.Profiles.Count > 0 || result.DefaultProfile != null ? result : null;
        }

        /// <summary>
        /// Find the profile for the given process name. Uses processNames in each profile; falls back to DefaultProfile.
        /// </summary>
        public static GameProfile? GetProfileForProcess(LoadedConfig? config, string? processName)
        {
            if (config == null)
                return null;

            if (!string.IsNullOrEmpty(processName))
            {
                var byProcess = config.Profiles.FirstOrDefault(p =>
                    p.ProcessNames != null && p.ProcessNames.Any(n =>
                        string.Equals(n, processName, StringComparison.OrdinalIgnoreCase)));
                if (byProcess != null)
                    return byProcess;
            }

            return config.DefaultProfile;
        }
    }
}
