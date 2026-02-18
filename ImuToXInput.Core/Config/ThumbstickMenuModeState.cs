namespace ImuToXInput.Config;

/// <summary>
/// Per-frame state for thumbstick menu mode. After the stick is held non-zero for a period,
/// output is forced to 0 until the user returns to the deadzone (like a physical stick snapping back).
/// Hold state per stick (left = X+Y, right = X+Y).
/// </summary>
public class ThumbstickMenuModeState
{
    /// <summary>Default hold duration before forcing stick to zero (ms).</summary>
    public const float DefaultHoldDurationMs = 500f;

    public bool LeftLocked { get; set; }
    public bool RightLocked { get; set; }
    public DateTime? LeftNonZeroSinceUtc { get; set; }
    public DateTime? RightNonZeroSinceUtc { get; set; }

    /// <summary>Per-axis offset (tracker position at last snap). When set, incoming values are relative to this center.</summary>
    public float? LeftThumbXOffset { get; set; }
    public float? LeftThumbYOffset { get; set; }
    public float? RightThumbXOffset { get; set; }
    public float? RightThumbYOffset { get; set; }
}

/// <summary>
/// State for edge-detecting the menu mode toggle condition (so we only toggle on rising edge).
/// </summary>
public class MenuModeToggleState
{
    public bool LastConditionValue { get; set; }
}
