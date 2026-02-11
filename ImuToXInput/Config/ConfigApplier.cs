using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;
using System.Linq;
using System.Numerics;
using ImuToXInput;

namespace ImuToXInput.Config
{
    /// <summary>
    /// Applies a JSON game profile to the virtual Xbox controller from current tracker state.
    /// </summary>
    public static class ConfigApplier
    {
        private const float DefaultDeadzone = 0.2f;

        public static void Apply(
            GameProfile profile,
            Dictionary<string, TrackerState> trackers,
            IXbox360Controller xbox,
            float axisDeadzone = DefaultDeadzone)
        {
            if (profile.Trackers != null && profile.Trackers.Count > 0)
            {
                // Only LEFT_FOOT and RIGHT_FOOT are valid for floor height; ignore any other joints.
                var floorTrackerIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "LEFT_FOOT", "RIGHT_FOOT" };
                var floorTrackers = profile.Trackers
                    .Where(id => floorTrackerIds.Contains(id))
                    .Select(id => trackers.TryGetValue(id, out var t) ? t : null)
                    .Where(t => t != null)
                    .Cast<TrackerState>()
                    .ToArray();
                if (floorTrackers.Length > 0)
                    TrackingEnvironment.UpdateFloor(floorTrackers);
            }

            foreach (var m in profile.AxisMappings)
            {
                if (!trackers.TryGetValue(m.Tracker, out var t)) continue;
                float raw = GetAxisSourceValue(t, m.Source) * m.Scale * (m.Invert ? -1f : 1f);
                if (!TryParseAxis(m.Axis, out var axis)) continue;
                xbox.SetAxisValue(axis, ApplyDeadzone(raw, axisDeadzone));
            }

            foreach (var m in profile.ButtonMappings)
            {
                bool value = EvaluateCondition(m.Condition, trackers);
                if (TryParseButton(m.Button, out var button))
                    xbox.SetButtonState(button, value);
            }

            foreach (var m in profile.TriggerMappings)
            {
                bool cond = EvaluateCondition(m.Condition, trackers);
                byte value = cond ? m.ValueWhenTrue : m.ValueWhenFalse;
                if (TryParseTrigger(m.Trigger, out var trigger))
                    xbox.SetSliderValue(trigger, value);
            }
        }

        public static short ApplyDeadzone(float value, float deadzone = DefaultDeadzone)
        {
            value = Math.Clamp(value / 15f, -1f, 1f);
            if (Math.Abs(value) < deadzone) return 0;
            float sign = Math.Sign(value);
            float scaled = (Math.Abs(value) - deadzone) / (1f - deadzone);
            return (short)(sign * scaled * 32767);
        }

        private static float GetAxisSourceValue(TrackerState t, string source)
        {
            return source switch
            {
                "EulerX" => t.Euler.X,
                "EulerY" => t.Euler.Y,
                "EulerZ" => t.Euler.Z,
                "PosX" => t.CalibratedPosition.X,
                "PosY" => t.CalibratedPosition.Y,
                "PosZ" => t.CalibratedPosition.Z,
                "FloorRelX" => t.FloorRelativePosition.X,
                "FloorRelY" => t.FloorRelativePosition.Y,
                "FloorRelZ" => t.FloorRelativePosition.Z,
                _ => 0
            };
        }

        private static float GetEulerComponent(TrackerState t, string component)
        {
            return component.ToUpperInvariant() switch
            {
                "X" => t.Euler.X,
                "Y" => t.Euler.Y,
                "Z" => t.Euler.Z,
                _ => 0
            };
        }

        private static float GetPositionComponent(TrackerState t, string source)
        {
            return source switch
            {
                "CalibratedX" => t.CalibratedPosition.X,
                "CalibratedY" => t.CalibratedPosition.Y,
                "CalibratedZ" => t.CalibratedPosition.Z,
                "FloorRelX" => t.FloorRelativePosition.X,
                "FloorRelY" => t.FloorRelativePosition.Y,
                "FloorRelZ" => t.FloorRelativePosition.Z,
                _ => 0
            };
        }

        private static bool EvalOp(float a, string op, float value)
        {
            return op switch
            {
                "less_than" or "lt" => a < value,
                "greater_than" or "gt" => a > value,
                "less_than_or_equal" or "lte" => a <= value,
                "greater_than_or_equal" or "gte" => a >= value,
                _ => false
            };
        }

        public static bool EvaluateCondition(MappingCondition? condition, Dictionary<string, TrackerState> trackers)
        {
            if (condition == null) return false;

            switch (condition)
            {
                case EulerThresholdCondition c:
                    if (!trackers.TryGetValue(c.Tracker, out var t1)) return false;
                    return EvalOp(GetEulerComponent(t1, c.Component), c.Op, c.Value);

                case EulerDiffCondition c:
                    if (!trackers.TryGetValue(c.TrackerA, out var ta) || !trackers.TryGetValue(c.TrackerB, out var tb))
                        return false;
                    float diff = GetEulerComponent(ta, c.Component) - GetEulerComponent(tb, c.Component);
                    return EvalOp(diff, c.Op, c.Value);

                case EulerSumCondition c:
                    if (!trackers.TryGetValue(c.TrackerA, out var sa) || !trackers.TryGetValue(c.TrackerB, out var sb))
                        return false;
                    float sum = GetEulerComponent(sa, c.Component) + GetEulerComponent(sb, c.Component);
                    return EvalOp(sum, c.Op, c.Value);

                case PositionThresholdCondition c:
                    if (!trackers.TryGetValue(c.Tracker, out var tp)) return false;
                    return EvalOp(GetPositionComponent(tp, c.Source), c.Op, c.Value);

                default:
                    return false;
            }
        }

        private static bool TryParseAxis(string name, out Xbox360Axis axis)
        {
            axis = Xbox360Axis.LeftThumbX;
            if (string.IsNullOrEmpty(name)) return false;
            var n = name.Trim();
            if (n.Equals("LeftThumbX", StringComparison.OrdinalIgnoreCase)) { axis = Xbox360Axis.LeftThumbX; return true; }
            if (n.Equals("LeftThumbY", StringComparison.OrdinalIgnoreCase)) { axis = Xbox360Axis.LeftThumbY; return true; }
            if (n.Equals("RightThumbX", StringComparison.OrdinalIgnoreCase)) { axis = Xbox360Axis.RightThumbX; return true; }
            if (n.Equals("RightThumbY", StringComparison.OrdinalIgnoreCase)) { axis = Xbox360Axis.RightThumbY; return true; }
            return false;
        }

        private static bool TryParseButton(string name, out Xbox360Button button)
        {
            button = Xbox360Button.A;
            if (string.IsNullOrEmpty(name)) return false;
            var n = name.Trim();
            if (n.Equals("A", StringComparison.OrdinalIgnoreCase)) { button = Xbox360Button.A; return true; }
            if (n.Equals("B", StringComparison.OrdinalIgnoreCase)) { button = Xbox360Button.B; return true; }
            if (n.Equals("X", StringComparison.OrdinalIgnoreCase)) { button = Xbox360Button.X; return true; }
            if (n.Equals("Y", StringComparison.OrdinalIgnoreCase)) { button = Xbox360Button.Y; return true; }
            if (n.Equals("LeftShoulder", StringComparison.OrdinalIgnoreCase)) { button = Xbox360Button.LeftShoulder; return true; }
            if (n.Equals("RightShoulder", StringComparison.OrdinalIgnoreCase)) { button = Xbox360Button.RightShoulder; return true; }
            if (n.Equals("Back", StringComparison.OrdinalIgnoreCase)) { button = Xbox360Button.Back; return true; }
            if (n.Equals("Start", StringComparison.OrdinalIgnoreCase)) { button = Xbox360Button.Start; return true; }
            if (n.Equals("Up", StringComparison.OrdinalIgnoreCase)) { button = Xbox360Button.Up; return true; }
            if (n.Equals("Down", StringComparison.OrdinalIgnoreCase)) { button = Xbox360Button.Down; return true; }
            if (n.Equals("Left", StringComparison.OrdinalIgnoreCase)) { button = Xbox360Button.Left; return true; }
            if (n.Equals("Right", StringComparison.OrdinalIgnoreCase)) { button = Xbox360Button.Right; return true; }
            return false;
        }

        private static bool TryParseTrigger(string name, out Xbox360Slider slider)
        {
            slider = Xbox360Slider.LeftTrigger;
            if (string.IsNullOrEmpty(name)) return false;
            var n = name.Trim();
            if (n.Equals("LeftTrigger", StringComparison.OrdinalIgnoreCase)) { slider = Xbox360Slider.LeftTrigger; return true; }
            if (n.Equals("RightTrigger", StringComparison.OrdinalIgnoreCase)) { slider = Xbox360Slider.RightTrigger; return true; }
            return false;
        }
    }
}
