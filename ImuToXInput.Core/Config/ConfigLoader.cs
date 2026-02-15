using Newtonsoft.Json;
using System.Linq;

namespace ImuToXInput.Config
{
    public class LoadedConfig
    {
        public List<GameProfile> Profiles { get; set; } = new();
        public GameProfile? DefaultProfile { get; set; }
    }

    public static class ConfigLoader
    {
        public const string ConfigFolderName = "configs";
        public const string DefaultConfigFileName = "default.json";

        public static string GetConfigDirectory()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ConfigFolderName);
        }

        /// <summary>Load config from the default config directory (e.g. app base + configs).</summary>
        public static LoadedConfig? Load()
        {
            return LoadFromDirectory(GetConfigDirectory());
        }

        /// <summary>Load config from a specific directory (e.g. MAUI FileSystem.AppDataDirectory/configs).</summary>
        public static LoadedConfig? LoadFromDirectory(string configDirectory)
        {
            if (string.IsNullOrEmpty(configDirectory) || !Directory.Exists(configDirectory))
                return null;

            var result = new LoadedConfig();
            var files = Directory.GetFiles(configDirectory, "*.json", SearchOption.TopDirectoryOnly);

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
