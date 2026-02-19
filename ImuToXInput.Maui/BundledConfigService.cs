using System.Reflection;

namespace ImuToXInput.Maui;

/// <summary>
/// Copies bundled .json config files from the app assembly to the config directory
/// when they are missing (e.g. first run). Does not overwrite existing files.
/// </summary>
public static class BundledConfigService
{
    private const string BundledPrefix = "ImuToXInput.Maui.BundledConfigs.";

    /// <summary>
    /// Ensures bundled configs exist in the given directory. Creates the directory if needed.
    /// Copies each embedded BundledConfigs/*.json only if the target file does not exist.
    /// </summary>
    public static void CopyBundledConfigsIfMissing(string configDirectory)
    {
        if (string.IsNullOrEmpty(configDirectory)) return;
        if (!Directory.Exists(configDirectory))
            Directory.CreateDirectory(configDirectory);

        var assembly = typeof(BundledConfigService).Assembly;
        var names = assembly.GetManifestResourceNames();
        foreach (var name in names)
        {
            if (!name.StartsWith(BundledPrefix, StringComparison.Ordinal) || !name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                continue;
            var fileName = name.Substring(BundledPrefix.Length);
            var targetPath = Path.Combine(configDirectory, fileName);
            if (File.Exists(targetPath))
                continue;
            try
            {
                using var stream = assembly.GetManifestResourceStream(name);
                if (stream == null) continue;
                using var file = File.Create(targetPath);
                stream.CopyTo(file);
            }
            catch
            {
                // ignore single-file copy failure
            }
        }
    }
}
