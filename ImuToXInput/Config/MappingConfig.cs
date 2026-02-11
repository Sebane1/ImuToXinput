using Newtonsoft.Json;

namespace ImuToXInput.Config
{
    /// <summary>
    /// One game config file (one .json per game in the configs folder). Which process names use it, and how to map trackers to XInput.
    /// </summary>
    public class GameProfile
    {
        [JsonProperty("name")]
        public string Name { get; set; } = "";

        /// <summary>
        /// Process names (no .exe) that select this profile. Empty or null = only used when name matches defaultProfile.
        /// </summary>
        [JsonProperty("processNames")]
        public List<string>? ProcessNames { get; set; }

        [JsonProperty("axisMappings")]
        public List<AxisMapping> AxisMappings { get; set; } = new();

        [JsonProperty("buttonMappings")]
        public List<ButtonMapping> ButtonMappings { get; set; } = new();

        [JsonProperty("triggerMappings")]
        public List<TriggerMapping> TriggerMappings { get; set; } = new();

        /// <summary>
        /// If set, UpdateFloor() is called with these trackers for floor-relative position.
        /// Only LEFT_FOOT and RIGHT_FOOT are valid; other joints are ignored at runtime.
        /// </summary>
        [JsonProperty("trackers")]
        public List<string>? Trackers { get; set; }
    }

    /// <summary>
    /// Maps one tracker component to one XInput axis (thumbsticks).
    /// </summary>
    public class AxisMapping
    {
        [JsonProperty("tracker")]
        public string Tracker { get; set; } = "";

        /// <summary>
        /// "EulerX", "EulerY", "EulerZ", "PosX", "PosY", "PosZ", "FloorRelX", "FloorRelY", "FloorRelZ"
        /// </summary>
        [JsonProperty("source")]
        public string Source { get; set; } = "";

        [JsonProperty("scale")]
        public float Scale { get; set; } = 1f;

        [JsonProperty("invert")]
        public bool Invert { get; set; }

        /// <summary>
        /// "LeftThumbX", "LeftThumbY", "RightThumbX", "RightThumbY"
        /// </summary>
        [JsonProperty("axis")]
        public string Axis { get; set; } = "";
    }

    /// <summary>
    /// Base for conditions that use one or two trackers.
    /// </summary>
    [JsonConverter(typeof(ConditionConverter))]
    public abstract class MappingCondition
    {
        /// <summary>
        /// "euler_threshold", "euler_diff", "euler_sum", "position_threshold"
        /// </summary>
        [JsonProperty("type")]
        public string Type { get; set; } = "";
    }

    /// <summary>
    /// Single tracker, e.g. leftFoot.Euler.X &lt; -20
    /// </summary>
    public class EulerThresholdCondition : MappingCondition
    {
        [JsonProperty("tracker")]
        public string Tracker { get; set; } = "";

        /// <summary>
        /// "X", "Y", "Z"
        /// </summary>
        [JsonProperty("component")]
        public string Component { get; set; } = "";

        /// <summary>
        /// "less_than", "greater_than", "less_than_or_equal", "greater_than_or_equal"
        /// </summary>
        [JsonProperty("op")]
        public string Op { get; set; } = "greater_than";

        [JsonProperty("value")]
        public float Value { get; set; }
    }

    /// <summary>
    /// Two trackers: trackerA.Component - trackerB.Component op value
    /// </summary>
    public class EulerDiffCondition : MappingCondition
    {
        [JsonProperty("trackerA")]
        public string TrackerA { get; set; } = "";

        [JsonProperty("trackerB")]
        public string TrackerB { get; set; } = "";

        [JsonProperty("component")]
        public string Component { get; set; } = "";

        [JsonProperty("op")]
        public string Op { get; set; } = "greater_than";

        [JsonProperty("value")]
        public float Value { get; set; }
    }

    /// <summary>
    /// Two trackers: trackerA.Component + trackerB.Component op value
    /// </summary>
    public class EulerSumCondition : MappingCondition
    {
        [JsonProperty("trackerA")]
        public string TrackerA { get; set; } = "";

        [JsonProperty("trackerB")]
        public string TrackerB { get; set; } = "";

        [JsonProperty("component")]
        public string Component { get; set; } = "";

        [JsonProperty("op")]
        public string Op { get; set; } = "greater_than";

        [JsonProperty("value")]
        public float Value { get; set; }
    }

    /// <summary>
    /// Position-based: e.g. rightHand.FloorRelativePosition.Y &gt; 0.1
    /// </summary>
    public class PositionThresholdCondition : MappingCondition
    {
        [JsonProperty("tracker")]
        public string Tracker { get; set; } = "";

        /// <summary>
        /// "CalibratedX", "CalibratedY", "CalibratedZ", "FloorRelX", "FloorRelY", "FloorRelZ"
        /// </summary>
        [JsonProperty("source")]
        public string Source { get; set; } = "";

        [JsonProperty("op")]
        public string Op { get; set; } = "greater_than";

        [JsonProperty("value")]
        public float Value { get; set; }
    }

    public class ButtonMapping
    {
        [JsonProperty("condition")]
        public MappingCondition Condition { get; set; } = null!;

        /// <summary>
        /// "A", "B", "X", "Y", "LeftShoulder", "RightShoulder", "Back", "Start", "Up", "Down", "Left", "Right"
        /// </summary>
        [JsonProperty("button")]
        public string Button { get; set; } = "";
    }

    public class TriggerMapping
    {
        [JsonProperty("condition")]
        public MappingCondition Condition { get; set; } = null!;

        /// <summary>
        /// "LeftTrigger", "RightTrigger"
        /// </summary>
        [JsonProperty("trigger")]
        public string Trigger { get; set; } = "";

        [JsonProperty("valueWhenTrue")]
        public byte ValueWhenTrue { get; set; } = 255;

        [JsonProperty("valueWhenFalse")]
        public byte ValueWhenFalse { get; set; } = 0;
    }
}
