using System.Linq;
using ImuToXInput;
using ImuToXInput.Core.Output;
using SlimeImuProtocol.SlimeProtocol;

namespace ImuToXInput.Config
{
    /// <summary>
    /// Applies a JSON game profile to an abstract gamepad output (ViGEm on Windows, BLE on Android).
    /// </summary>
    public static class ConfigApplier
    {
        private const float DefaultDeadzone = 0.2f;

        public static void Apply(
            GameProfile profile,
            Dictionary<string, TrackerState> trackers,
            IGamepadOutput output,
            float axisDeadzone = DefaultDeadzone)
        {
            if (profile.Trackers != null && profile.Trackers.Count > 0)
            {
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
                output.SetAxis(axis, ApplyDeadzone(raw, axisDeadzone));
            }

            foreach (var m in profile.ButtonMappings)
            {
                bool value = EvaluateCondition(m.Condition, trackers);
                if (TryParseButton(m.Button, out var button))
                    output.SetButton(button, value);
            }

            foreach (var m in profile.TriggerMappings)
            {
                bool cond = EvaluateCondition(m.Condition, trackers);
                byte value = cond ? m.ValueWhenTrue : m.ValueWhenFalse;
                if (TryParseTrigger(m.Trigger, out var trigger))
                    output.SetTrigger(trigger, value);
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

        private static bool TryParseAxis(string name, out GamepadAxis axis)
        {
            axis = GamepadAxis.LeftThumbX;
            if (string.IsNullOrEmpty(name)) return false;
            var n = name.Trim();
            if (n.Equals("LeftThumbX", StringComparison.OrdinalIgnoreCase)) { axis = GamepadAxis.LeftThumbX; return true; }
            if (n.Equals("LeftThumbY", StringComparison.OrdinalIgnoreCase)) { axis = GamepadAxis.LeftThumbY; return true; }
            if (n.Equals("RightThumbX", StringComparison.OrdinalIgnoreCase)) { axis = GamepadAxis.RightThumbX; return true; }
            if (n.Equals("RightThumbY", StringComparison.OrdinalIgnoreCase)) { axis = GamepadAxis.RightThumbY; return true; }
            return false;
        }

        private static bool TryParseButton(string name, out GamepadButton button)
        {
            button = GamepadButton.A;
            if (string.IsNullOrEmpty(name)) return false;
            var n = name.Trim();
            if (n.Equals("A", StringComparison.OrdinalIgnoreCase)) { button = GamepadButton.A; return true; }
            if (n.Equals("B", StringComparison.OrdinalIgnoreCase)) { button = GamepadButton.B; return true; }
            if (n.Equals("X", StringComparison.OrdinalIgnoreCase)) { button = GamepadButton.X; return true; }
            if (n.Equals("Y", StringComparison.OrdinalIgnoreCase)) { button = GamepadButton.Y; return true; }
            if (n.Equals("LeftShoulder", StringComparison.OrdinalIgnoreCase)) { button = GamepadButton.LeftShoulder; return true; }
            if (n.Equals("RightShoulder", StringComparison.OrdinalIgnoreCase)) { button = GamepadButton.RightShoulder; return true; }
            if (n.Equals("Back", StringComparison.OrdinalIgnoreCase)) { button = GamepadButton.Back; return true; }
            if (n.Equals("Start", StringComparison.OrdinalIgnoreCase)) { button = GamepadButton.Start; return true; }
            if (n.Equals("Up", StringComparison.OrdinalIgnoreCase)) { button = GamepadButton.Up; return true; }
            if (n.Equals("Down", StringComparison.OrdinalIgnoreCase)) { button = GamepadButton.Down; return true; }
            if (n.Equals("Left", StringComparison.OrdinalIgnoreCase)) { button = GamepadButton.Left; return true; }
            if (n.Equals("Right", StringComparison.OrdinalIgnoreCase)) { button = GamepadButton.Right; return true; }
            return false;
        }

        private static bool TryParseTrigger(string name, out GamepadTrigger trigger)
        {
            trigger = GamepadTrigger.LeftTrigger;
            if (string.IsNullOrEmpty(name)) return false;
            var n = name.Trim();
            if (n.Equals("LeftTrigger", StringComparison.OrdinalIgnoreCase)) { trigger = GamepadTrigger.LeftTrigger; return true; }
            if (n.Equals("RightTrigger", StringComparison.OrdinalIgnoreCase)) { trigger = GamepadTrigger.RightTrigger; return true; }
            return false;
        }
    }
}
