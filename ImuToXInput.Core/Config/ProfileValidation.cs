namespace ImuToXInput.Config;

/// <summary>
/// Validation helpers for GameProfile. Used by editors to warn about potential issues.
/// </summary>
public static class ProfileValidation
{
    /// <summary>
    /// Returns warnings for duplicate mappings (multiple mappings targeting the same axis, button, or trigger).
    /// Duplicates are allowed at runtime (triggers use max; axes/buttons overwrite), but usually indicate a mistake.
    /// </summary>
    public static List<string> GetDuplicateMappingWarnings(GameProfile profile)
    {
        var warnings = new List<string>();
        if (profile == null) return warnings;

        var axisTargets = profile.AxisMappings
            .Where(m => !string.IsNullOrEmpty(m.Axis))
            .GroupBy(m => m.Axis.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1);
        foreach (var g in axisTargets)
            warnings.Add($"Axis {g.Key}: {g.Count()} mappings (last overwrites; may be intentional).");

        var buttonTargets = profile.ButtonMappings
            .Where(m => !string.IsNullOrEmpty(m.Button))
            .GroupBy(m => m.Button.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1);
        foreach (var g in buttonTargets)
            warnings.Add($"Button {g.Key}: {g.Count()} mappings (last overwrites; may be intentional).");

        var triggerTargets = profile.TriggerMappings
            .Where(m => !string.IsNullOrEmpty(m.Trigger))
            .GroupBy(m => m.Trigger.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1);
        foreach (var g in triggerTargets)
            warnings.Add($"Trigger {g.Key}: {g.Count()} mappings (highest value wins; may be intentional).");

        return warnings;
    }
}
