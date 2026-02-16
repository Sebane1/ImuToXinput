using Newtonsoft.Json;
using System.Linq;

namespace ImuToXInput.Config
{
    public class LoadedConfig
    {
        public List<GameProfile> Profiles { get; set; } = new();
        /// <summary>File name for each profile in Profiles (same order).</summary>
        public List<string> ProfileFileNames { get; set; } = new();
        public GameProfile? DefaultProfile { get; set; }
        /// <summary>Config directory path; used for primary profile lookup.</summary>
        public string ConfigDirectory { get; set; } = "";
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
            {
                return null;
            }

            var result = new LoadedConfig { ConfigDirectory = configDirectory };
            var files = Directory.GetFiles(configDirectory, "*.json", SearchOption.TopDirectoryOnly);

            foreach (var path in files)
            {
                try
                {
                    var fileName = Path.GetFileName(path);
                    if (string.Equals(fileName, PrimaryProfilesPreferences.FileName, StringComparison.OrdinalIgnoreCase))
                    {
                        continue; // Skip primaryProfiles.json
                    }
                    var json = File.ReadAllText(path);
                    var profile = JsonConvert.DeserializeObject<GameProfile>(json);
                    if (profile == null)
                    {
                        continue;
                    }

                    if (string.Equals(fileName, DefaultConfigFileName, StringComparison.OrdinalIgnoreCase))
                    {
                        result.DefaultProfile = profile;
                    }
                    else
                    {
                        result.Profiles.Add(profile);
                        result.ProfileFileNames.Add(fileName);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to load config {Path.GetFileName(path)}: {ex.Message}");
                }
            }

            if (result.DefaultProfile == null && result.Profiles.Count > 0)
            {
                result.DefaultProfile = result.Profiles.FirstOrDefault(p =>
                    string.Equals(p.Name, "default", StringComparison.OrdinalIgnoreCase)) ?? result.Profiles[0];
            }

            return result.Profiles.Count > 0 || result.DefaultProfile != null ? result : null;
        }

        /// <summary>Get the profile to use for the running process. Uses primary profile preference when multiple match.</summary>
        public static GameProfile? GetProfileForProcess(LoadedConfig? config, string? processName)
        {
            if (config == null)
                return null;

            if (!string.IsNullOrEmpty(processName))
            {
                var matching = config.Profiles
                    .Select((p, i) => (Profile: p, FileName: i < config.ProfileFileNames.Count ? config.ProfileFileNames[i] : ""))
                    .Where(x => x.Profile.ProcessNames != null && x.Profile.ProcessNames.Any(n =>
                        string.Equals(n, processName, StringComparison.OrdinalIgnoreCase)))
                    .ToList();
                if (matching.Count > 0)
                {
                    if (matching.Count == 1)
                    {
                        return matching[0].Profile;
                    }
                    var primary = string.IsNullOrEmpty(config.ConfigDirectory)
                        ? null
                        : PrimaryProfilesPreferences.GetPrimaryProfile(config.ConfigDirectory, processName);
                    if (!string.IsNullOrEmpty(primary))
                    {
                        var chosen = matching.FirstOrDefault(m =>
                            string.Equals(m.FileName, primary, StringComparison.OrdinalIgnoreCase));
                        if (chosen.Profile != null)
                        {
                            return chosen.Profile;
                        }
                    }
                    return matching[0].Profile;
                }
            }

            return config.DefaultProfile;
        }
    }
}
