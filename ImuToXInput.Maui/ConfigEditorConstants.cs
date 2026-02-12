namespace ImuToXInput.Maui;

/// <summary>
/// Dropdown/validation values for the config editor (shared with WinForms editor).
/// </summary>
public static class ConfigEditorConstants
{
    public static readonly string[] TrackerIds =
    {
        "HEAD", "CHEST", "HIP",
        "LEFT_UPPER_ARM", "RIGHT_UPPER_ARM", "LEFT_LOWER_ARM", "RIGHT_LOWER_ARM",
        "LEFT_HAND", "RIGHT_HAND",
        "LEFT_UPPER_LEG", "RIGHT_UPPER_LEG", "LEFT_LOWER_LEG", "RIGHT_LOWER_LEG",
        "LEFT_FOOT", "RIGHT_FOOT"
    };

    public static readonly string[] FloorTrackerIds = { "LEFT_FOOT", "RIGHT_FOOT" };

    public static readonly string[] AxisSources =
    {
        "EulerX", "EulerY", "EulerZ",
        "PosX", "PosY", "PosZ",
        "FloorRelX", "FloorRelY", "FloorRelZ"
    };

    public static readonly string[] AxisOutputs =
    {
        "LeftThumbX", "LeftThumbY", "RightThumbX", "RightThumbY"
    };

    public static readonly string[] EulerComponents = { "X", "Y", "Z" };

    public static readonly string[] PositionSources =
    {
        "CalibratedX", "CalibratedY", "CalibratedZ",
        "FloorRelX", "FloorRelY", "FloorRelZ"
    };

    public static readonly string[] Operators =
    {
        "less_than", "greater_than", "less_than_or_equal", "greater_than_or_equal"
    };

    public static readonly string[] Buttons =
    {
        "A", "B", "X", "Y",
        "LeftShoulder", "RightShoulder", "Back", "Start",
        "Up", "Down", "Left", "Right"
    };

    public static readonly string[] Triggers = { "LeftTrigger", "RightTrigger" };

    public static readonly string[] ConditionTypes =
    {
        "euler_threshold", "euler_diff", "euler_sum", "position_threshold"
    };
}
