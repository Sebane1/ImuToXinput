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
        public const string MenuProfileFileName = "menu.json";

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

        /// <summary>Get the menu mode profile. Loads menu.json from configDirectory if present; otherwise returns a built-in default (head→right stick, hip→left stick).</summary>
        public static GameProfile GetMenuProfile(string configDirectory)
        {
            var menuPath = Path.Combine(configDirectory, MenuProfileFileName);
            if (File.Exists(menuPath))
            {
                try
                {
                    var json = File.ReadAllText(menuPath);
                    var profile = JsonConvert.DeserializeObject<GameProfile>(json);
                    if (profile != null) return profile;
                }
                catch { /* fall through to built-in */ }
            }
            return GetBuiltInMenuProfile();
        }

        private static GameProfile GetBuiltInMenuProfile()
        {
            return new GameProfile
            {
                Name = "Menu",
                AxisMappings = new List<AxisMapping>
                {
                    new() { Tracker = "HEAD", Source = "EulerX", Scale = 2f, Invert = true, Axis = "LeftThumbY" },
                    new() { Tracker = "HEAD", Source = "EulerY", Scale = 1f, Invert = true, Axis = "LeftThumbX" },
                },
                ButtonMappings = new List<ButtonMapping>()
                {
                    new() { Condition = new EulerThresholdCondition { Tracker = "RIGHT_FOOT", Component = "X", Op = "less_than", Value = -20f }, Button = "A" },
                    new() { Condition = new EulerThresholdCondition { Tracker = "LEFT_FOOT", Component = "X", Op = "less_than", Value = -20f }, Button = "B" },
                },
                TriggerMappings = new List<TriggerMapping>()
            };
        }

        /// <summary>Get the profile to use for the running process. Uses primary profile preference when multiple match.</summary>
        public static GameProfile? GetProfileForProcess(LoadedConfig? config, string? processName)
        {
            var (profile, _) = GetProfileAndFileNameForProcess(config, processName);
            return profile;
        }

        /// <summary>Get the profile and its file name for the running process. File name is null when using legacy game mode.</summary>
        public static (GameProfile? profile, string? fileName) GetProfileAndFileNameForProcess(LoadedConfig? config, string? processName)
        {
            if (config == null)
            {
                return (null, null);
            }

            if (!string.IsNullOrEmpty(processName))
            {
                var matching = config.Profiles
                    .Select((p, i) => (Profile: p, FileName: i < config.ProfileFileNames.Count ? config.ProfileFileNames[i] : ""))
                    .Where(x => x.Profile.ProcessNames != null && x.Profile.ProcessNames.Any(n =>
                        string.Equals(n, processName, StringComparison.OrdinalIgnoreCase)))
                    .ToList();
                if (matching.Count > 0)
                {
                    var primary = string.IsNullOrEmpty(config.ConfigDirectory)
                        ? null
                        : PrimaryProfilesPreferences.GetPrimaryProfile(config.ConfigDirectory, processName);
                    var chosen = matching.Count == 1
                        ? matching[0]
                        : matching.FirstOrDefault(m => string.Equals(m.FileName, primary, StringComparison.OrdinalIgnoreCase));
                    if (chosen.Profile == null)
                    {
                        chosen = matching[0];
                    }
                    return (chosen.Profile, chosen.FileName);
                }
            }

            return (config.DefaultProfile, config.DefaultProfile != null ? DefaultConfigFileName : null);
        }

        /// <summary>All process names referenced by any profile (for process detection). Includes "stepmania" for legacy mode.</summary>
        public static IEnumerable<string> GetAllProcessNames(LoadedConfig? config)
        {
            if (config == null)
            {
                yield break;
            }
            yield return "stepmania";
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (config.DefaultProfile?.ProcessNames != null)
            {
                foreach (var n in config.DefaultProfile.ProcessNames)
                {
                    if (!string.IsNullOrEmpty(n) && seen.Add(n))
                    {
                        yield return n;
                    }
                }
            }
            foreach (var p in config.Profiles)
            {
                if (p.ProcessNames == null)
                {
                    continue;
                }
                foreach (var n in p.ProcessNames)
                {
                    if (!string.IsNullOrEmpty(n) && seen.Add(n))
                    {
                        yield return n;
                    }
                }
            }
        }
    }
}
