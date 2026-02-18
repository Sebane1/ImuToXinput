using System.Linq;
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
        /// <summary>Menu mode: deadzone for unlocking stick after snap-to-neutral. Larger = more forgiving return to center.</summary>
        private const float MenuModeUnlockDeadzone = 0.35f;

        public static void Apply(
            GameProfile profile,
            Dictionary<string, TrackerState> trackers,
            IGamepadOutput output,
            float axisDeadzone = DefaultDeadzone,
            bool menuMode = false,
            ThumbstickMenuModeState? menuModeState = null,
            float menuModeHoldMs = ThumbstickMenuModeState.DefaultHoldDurationMs)
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
                {
                    TrackingEnvironment.UpdateFloor(floorTrackers);
                }
            }

            // Build raw values and deadzone per thumbstick axis (from profile mappings; 0 if unmapped)
            var rawByAxis = new Dictionary<GamepadAxis, float>
            {
                { GamepadAxis.LeftThumbX, 0 }, { GamepadAxis.LeftThumbY, 0 },
                { GamepadAxis.RightThumbX, 0 }, { GamepadAxis.RightThumbY, 0 }
            };
            var deadzoneByAxis = new Dictionary<GamepadAxis, float>();
            foreach (var m in profile.AxisMappings)
            {
                if (!trackers.TryGetValue(m.Tracker, out var t)) continue;
                if (!TryParseAxis(m.Axis, out var axis)) continue;
                float raw = GetAxisSourceValue(t, m.Source) * m.Scale * (m.Invert ? -1f : 1f);
                if (menuMode && menuModeState != null)
                {
                    var off = GetOffset(menuModeState, axis);
                    if (off.HasValue) raw -= off.Value;
                }
                rawByAxis[axis] = raw;
                deadzoneByAxis[axis] = m.Deadzone ?? axisDeadzone;
            }

            // Apply deadzone (per-axis if configured)
            var outByAxis = new Dictionary<GamepadAxis, short>();
            foreach (var kv in rawByAxis)
            {
                var dz = deadzoneByAxis.TryGetValue(kv.Key, out var d) ? d : axisDeadzone;
                outByAxis[kv.Key] = ApplyDeadzone(kv.Value, dz);
            }

            // Menu mode: per-stick lock after hold duration, unlock when raw in deadzone; on lock, set new center offset
            if (menuMode && menuModeState != null)
            {
                var now = DateTime.UtcNow;
                bool leftLocked = menuModeState.LeftLocked;
                DateTime? leftNonZero = menuModeState.LeftNonZeroSinceUtc;
                ApplyMenuModeStick(menuModeState, GamepadAxis.LeftThumbX, GamepadAxis.LeftThumbY, rawByAxis, outByAxis, axisDeadzone, menuModeHoldMs, now, ref leftLocked, ref leftNonZero);
                menuModeState.LeftLocked = leftLocked;
                menuModeState.LeftNonZeroSinceUtc = leftNonZero;
                bool rightLocked = menuModeState.RightLocked;
                DateTime? rightNonZero = menuModeState.RightNonZeroSinceUtc;
                ApplyMenuModeStick(menuModeState, GamepadAxis.RightThumbX, GamepadAxis.RightThumbY, rawByAxis, outByAxis, axisDeadzone, menuModeHoldMs, now, ref rightLocked, ref rightNonZero);
                menuModeState.RightLocked = rightLocked;
                menuModeState.RightNonZeroSinceUtc = rightNonZero;
            }

            foreach (var kv in outByAxis)
                output.SetAxis(kv.Key, kv.Value);

            foreach (var m in profile.ButtonMappings)
            {
                bool value = EvaluateCondition(m.Condition, trackers);
                if (TryParseButton(m.Button, out var button))
                {
                    output.SetButton(button, value);
                }
            }

            ApplyTriggerMappings(profile, trackers, output, axisDeadzone);
        }

        /// <summary>
        /// When multiple trigger mappings target the same trigger, the effective value is the maximum of all their values (0–255).
        /// So the strongest input wins: e.g. axis gives 80, conditional gives 255 when true → trigger gets 255.
        /// </summary>
        private static void ApplyTriggerMappings(
            GameProfile profile,
            Dictionary<string, TrackerState> trackers,
            IGamepadOutput output,
            float axisDeadzone)
        {
            var triggerValues = new Dictionary<GamepadTrigger, byte>
            {
                { GamepadTrigger.LeftTrigger, 0 },
                { GamepadTrigger.RightTrigger, 0 }
            };

            foreach (var m in profile.TriggerMappings)
            {
                if (!TryParseTrigger(m.Trigger, out var trigger)) continue;

                byte value;
                if (m.FixedValue.HasValue)
                {
                    value = m.FixedValue.Value;
                }
                else if (!string.IsNullOrEmpty(m.Tracker) && !string.IsNullOrEmpty(m.Source) && trackers.TryGetValue(m.Tracker, out var triggerTracker))
                {
                    float raw = GetAxisSourceValue(triggerTracker, m.Source) * m.Scale * (m.Invert ? -1f : 1f);
                    value = FloatToTrigger(raw, m.Deadzone ?? axisDeadzone);
                }
                else if (m.Condition != null)
                {
                    bool cond = EvaluateCondition(m.Condition, trackers);
                    value = cond ? m.ValueWhenTrue : m.ValueWhenFalse;
                }
                else
                    continue;

                if (value > triggerValues[trigger])
                {
                    triggerValues[trigger] = value;
                }
            }

            foreach (var kv in triggerValues)
                output.SetTrigger(kv.Key, kv.Value);
        }

        private static float NormalizedRaw(float raw)
        {
            return Math.Clamp(raw / 15f, -1f, 1f);
        }

        private static float? GetOffset(ThumbstickMenuModeState state, GamepadAxis axis)
        {
            return axis switch
            {
                GamepadAxis.LeftThumbX => state.LeftThumbXOffset,
                GamepadAxis.LeftThumbY => state.LeftThumbYOffset,
                GamepadAxis.RightThumbX => state.RightThumbXOffset,
                GamepadAxis.RightThumbY => state.RightThumbYOffset,
                _ => null
            };
        }

        private static void SetOffset(ThumbstickMenuModeState state, GamepadAxis axis, float value)
        {
            switch (axis)
            {
                case GamepadAxis.LeftThumbX: state.LeftThumbXOffset = value; break;
                case GamepadAxis.LeftThumbY: state.LeftThumbYOffset = value; break;
                case GamepadAxis.RightThumbX: state.RightThumbXOffset = value; break;
                case GamepadAxis.RightThumbY: state.RightThumbYOffset = value; break;
            }
        }

        private static void ApplyMenuModeStick(
            ThumbstickMenuModeState menuModeState,
            GamepadAxis axisX, GamepadAxis axisY,
            Dictionary<GamepadAxis, float> rawByAxis,
            Dictionary<GamepadAxis, short> outByAxis,
            float deadzone, float holdMs, DateTime nowUtc,
            ref bool locked, ref DateTime? nonZeroSinceUtc)
        {
            float rawX = rawByAxis.TryGetValue(axisX, out var rx) ? rx : 0;
            float rawY = rawByAxis.TryGetValue(axisY, out var ry) ? ry : 0;
            float nX = NormalizedRaw(rawX);
            float nY = NormalizedRaw(rawY);
            bool inDeadzone = Math.Abs(nX) < MenuModeUnlockDeadzone && Math.Abs(nY) < MenuModeUnlockDeadzone;
            short outX = outByAxis[axisX];
            short outY = outByAxis[axisY];
            bool outputNonZero = outX != 0 || outY != 0;

            if (locked)
            {
                if (inDeadzone)
                {
                    locked = false;
                }
                else
                {
                    outByAxis[axisX] = 0;
                    outByAxis[axisY] = 0;
                }
                return;
            }
            if (outputNonZero)
            {
                if (nonZeroSinceUtc == null)
                {
                    nonZeroSinceUtc = nowUtc;
                }
                if (nonZeroSinceUtc != null && (nowUtc - nonZeroSinceUtc.Value).TotalMilliseconds >= holdMs)
                {
                    locked = true;
                    nonZeroSinceUtc = null;
                    outByAxis[axisX] = 0;
                    outByAxis[axisY] = 0;
                    // Set new center offset: current physical position becomes zero
                    float physX = rawX + (GetOffset(menuModeState, axisX) ?? 0);
                    float physY = rawY + (GetOffset(menuModeState, axisY) ?? 0);
                    SetOffset(menuModeState, axisX, physX);
                    SetOffset(menuModeState, axisY, physY);
                }
            }
            else
            {
                nonZeroSinceUtc = null;
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

        /// <summary>Maps a raw axis value (e.g. degrees) to trigger 0-255. Same normalization as axes (÷15, clamp -1..1), then linear map to 0..255. Deadzone maps to 0.</summary>
        public static byte FloatToTrigger(float value, float deadzone = DefaultDeadzone)
        {
            float n = Math.Clamp(value / 15f, -1f, 1f);
            if (Math.Abs(n) < deadzone) return 0;
            float t = (n + 1f) * 0.5f; // 0..1
            return (byte)Math.Clamp((int)(t * 255f), 0, 255);
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
                    {
                        return false;
                    }
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
