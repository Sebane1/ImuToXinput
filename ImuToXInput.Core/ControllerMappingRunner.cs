using System.Numerics;
using ImuToXInput.Core.Output;
using SlimeImuProtocol.SlimeProtocol;

namespace ImuToXInput.Config;

/// <summary>
/// Shared controller update logic: applies config profile or legacy game modes to gamepad output.
/// Used by both the desktop app and MAUI (Android BLE / Windows ViGEm).
/// </summary>
public static class ControllerMappingRunner
{
    private static readonly object StepManiaMarker = new();
    private static object? _lastAppliedKey;

    private static void ClearAllInputsIfProfileSwitched(object? newKey, IGamepadOutput output)
    {
        if (ReferenceEquals(_lastAppliedKey, newKey)) return;
        _lastAppliedKey = newKey;
        ClearAllInputs(output);
    }

    private static void ClearAllInputs(IGamepadOutput output)
    {
        foreach (GamepadAxis axis in Enum.GetValues<GamepadAxis>())
            output.SetAxis(axis, 0);
        foreach (GamepadButton button in Enum.GetValues<GamepadButton>())
            output.SetButton(button, false);
        foreach (GamepadTrigger trigger in Enum.GetValues<GamepadTrigger>())
            output.SetTrigger(trigger, 0);
    }

    /// <summary>
    /// Run one update: apply active profile override, or resolve profile by process name, or use legacy game mode.
    /// </summary>
    /// <param name="trackers">Body-part → TrackerState (from SlimeVR client).</param>
    /// <param name="output">Gamepad output (ViGEm or BLE).</param>
    /// <param name="loadedConfig">Loaded configs; can be null.</param>
    /// <param name="getProcessName">Returns current game process name (desktop); MAUI can pass () => null.</param>
    /// <param name="activeProfileOverride">When set (e.g. MAUI active profile), use this profile and ignore process detection.</param>
    /// <param name="menuMode">When true, thumbsticks snap to neutral after a hold period until user returns to deadzone.</param>
    /// <param name="menuModeState">State for menu mode; must be non-null when menuMode is true and using a profile.</param>
    /// <param name="onMenuModeToggleRequested">When the profile's menuModeToggle condition fires (rising edge), this is invoked. Does not send controller input.</param>
    /// <param name="menuModeToggleState">State for edge-detecting the menu mode toggle; pass non-null when using onMenuModeToggleRequested.</param>
    public static void Update(
        Dictionary<string, TrackerState> trackers,
        IGamepadOutput output,
        LoadedConfig? loadedConfig,
        Func<string?> getProcessName,
        GameProfile? activeProfileOverride,
        bool menuMode = false,
        ThumbstickMenuModeState? menuModeState = null,
        Action? onMenuModeToggleRequested = null,
        MenuModeToggleState? menuModeToggleState = null)
    {
        string? runningGame = getProcessName();
        GameProfile? profileToApply = null;
        if (activeProfileOverride != null)
        {
            profileToApply = activeProfileOverride;
        }

        if (profileToApply == null)
        {
            if (string.Equals(runningGame, "stepmania", StringComparison.OrdinalIgnoreCase))
            {
                ClearAllInputsIfProfileSwitched(StepManiaMarker, output);
                StepMania(trackers, output);
                return;
            }
            if (loadedConfig != null)
            {
                profileToApply = ConfigLoader.GetProfileForProcess(loadedConfig, runningGame);
            }
        }

        if (profileToApply != null)
        {
            ClearAllInputsIfProfileSwitched(profileToApply, output);
            if (profileToApply.MenuModeToggleCondition != null && menuModeToggleState != null && onMenuModeToggleRequested != null)
            {
                bool current = ConfigApplier.EvaluateCondition(profileToApply.MenuModeToggleCondition, trackers);
                if (current && !menuModeToggleState.LastConditionValue)
                {
                    onMenuModeToggleRequested();
                }
                menuModeToggleState.LastConditionValue = current;
            }
            ConfigApplier.Apply(profileToApply, trackers, output, menuMode: menuMode, menuModeState: menuModeState);
            return;
        }

        ClearAllInputsIfProfileSwitched(null, output);

        //switch (runningGame)
        //{
        //    case "MirrorsEdge":
        //        MirrorsEdge(trackers, output);
        //        break;
        //    case "ffxiv_dx11":
        //        FFXIV(trackers, output);
        //        break;
        //    case "portal":
        //    case "portal2":
        //        Portal(trackers, output);
        //        break;
        //    default:
        //        FPS(trackers, output);
        //        break;
        //}
    }

    private static short ApplyDeadzone(float value, float deadzone = 0.2f)
    {
        value = Math.Clamp(value / 15f, -1f, 1f);
        if (Math.Abs(value) < deadzone) return 0;
        float sign = Math.Sign(value);
        float scaled = (Math.Abs(value) - deadzone) / (1f - deadzone);
        return (short)(sign * scaled * 32767);
    }

    private static void Portal(Dictionary<string, TrackerState> trackers, IGamepadOutput output)
    {
        TrackerState? chest = null;
        trackers.TryGetValue("CHEST", out chest);

        if (trackers.TryGetValue("HEAD", out var head))
        {
            output.SetAxis(GamepadAxis.RightThumbY, ApplyDeadzone(-head.Euler.X * 2));
            output.SetAxis(GamepadAxis.RightThumbX, ApplyDeadzone(-head.Euler.Y * 1f));
        }
        if (trackers.TryGetValue("HIP", out var hips))
        {
            output.SetAxis(GamepadAxis.LeftThumbX, ApplyDeadzone(hips.Euler.Z * 2));
            output.SetAxis(GamepadAxis.LeftThumbY, ApplyDeadzone(-hips.Euler.X * 2));
        }
        if (chest != null && trackers.TryGetValue("RIGHT_UPPER_ARM", out var rightHand))
        {
            output.SetButton(GamepadButton.B, rightHand.Euler.Y - chest.Euler.Y > 30f);
            output.SetTrigger(GamepadTrigger.RightTrigger, (byte)(rightHand.Euler.Y + chest.Euler.Y < -15f ? 255 : 0));
        }
        if (chest != null && trackers.TryGetValue("LEFT_UPPER_ARM", out var leftHand))
        {
            bool triggerValue = leftHand.Euler.Y + chest.Euler.Y > 10f;
            output.SetTrigger(GamepadTrigger.LeftTrigger, (byte)(triggerValue ? 255 : 0));
        }
        if (trackers.TryGetValue("LEFT_FOOT", out var leftFoot))
        {
            output.SetButton(GamepadButton.A, leftFoot.Euler.X < -20);
            output.SetButton(GamepadButton.LeftShoulder, leftFoot.Euler.Y > 5);
        }
        if (trackers.TryGetValue("RIGHT_FOOT", out var rightFoot))
        {
            output.SetButton(GamepadButton.A, rightFoot.Euler.X < -20);
            output.SetButton(GamepadButton.RightShoulder, rightFoot.Euler.Y < -5);
        }
    }

    private static (bool up, bool down, bool left, bool right, bool upLeft, bool upRight, bool downLeft, bool downRight)
        GetFootDirection(TrackerState ankle, float deadZone, float diagThreshold, float cardThreshold)
    {
        float x = ankle.CalibratedPosition.X;
        float z = ankle.CalibratedPosition.Z;

        if (Math.Abs(x) < deadZone && Math.Abs(z) < deadZone)
        {
            return (false, false, false, false, false, false, false, false);
        }

        var dir = new Vector2(x, z);
        float mag = dir.Length();

        if (mag < deadZone)
        {
            return (false, false, false, false, false, false, false, false);
        }

        dir /= mag;

        if (ankle.CloseToCalibratedY)
        {
            if (mag > diagThreshold)
            {
                if (dir.X < -0.5f && -dir.Y < -0.5f) return (false, false, false, false, true, false, false, false);
                if (dir.X > 0.5f && -dir.Y < -0.5f) return (false, false, false, false, false, true, false, false);
                if (dir.X < -0.5f && -dir.Y > 0.5f) return (false, false, false, false, false, false, true, false);
                if (dir.X > 0.5f && -dir.Y > 0.5f) return (false, false, false, false, false, false, false, true);
            }
            if (Math.Abs(dir.Y) > Math.Abs(dir.X))
            {
                if (dir.Y < -cardThreshold) { return (false, true, false, false, false, false, false, false); }
                if (dir.Y > cardThreshold) { return (true, false, false, false, false, false, false, false); }
            }
            else
            {
                if (dir.X < -cardThreshold) return (false, false, true, false, false, false, false, false);
                if (dir.X > cardThreshold) return (false, false, false, true, false, false, false, false);
            }
        }
        return (false, false, false, false, false, false, false, false);
    }

    private static void StepMania(Dictionary<string, TrackerState> trackers, IGamepadOutput output)
    {
        TrackerState? leftAnkle = null, rightAnkle = null;
        bool hasFeet = (trackers.TryGetValue("LEFT_FOOT", out leftAnkle) && trackers.TryGetValue("RIGHT_FOOT", out rightAnkle))
            || (trackers.TryGetValue("LEFT_LOWER_LEG", out leftAnkle) && trackers.TryGetValue("RIGHT_LOWER_LEG", out rightAnkle));
        if (!hasFeet || leftAnkle == null || rightAnkle == null)
        {
            return;
        }

        TrackingEnvironment.UpdateFloor(leftAnkle, rightAnkle);

        float deadZone = 0.200f;
        float diagThreshold = 0.35f;
        float cardThreshold = 0.50f;

        var leftDir = GetFootDirection(leftAnkle, deadZone, diagThreshold, cardThreshold);
        var rightDir = GetFootDirection(rightAnkle, deadZone, diagThreshold, cardThreshold);

        bool upState = leftDir.up || rightDir.up;
        bool downState = leftDir.down || rightDir.down;
        bool leftState = leftDir.left || rightDir.left;
        bool rightState = leftDir.right || rightDir.right;
        bool upLeftState = leftDir.upLeft;
        bool downLeftState = leftDir.downLeft;
        bool upRightState = rightDir.upRight;
        bool downRightState = rightDir.downRight;

        output.SetButton(GamepadButton.Up, upState);
        output.SetButton(GamepadButton.Down, downState);
        output.SetButton(GamepadButton.Left, leftState);
        output.SetButton(GamepadButton.Right, rightState);
        output.SetButton(GamepadButton.A, upRightState);
        output.SetButton(GamepadButton.B, upLeftState);
        output.SetButton(GamepadButton.X, downRightState);
        output.SetButton(GamepadButton.Y, downLeftState);
    }

    private static void MirrorsEdge(Dictionary<string, TrackerState> trackers, IGamepadOutput output)
    {
        TrackerState? chest = null;
        trackers.TryGetValue("CHEST", out chest);

        if (trackers.TryGetValue("HEAD", out var head))
        {
            output.SetAxis(GamepadAxis.RightThumbY, ApplyDeadzone(-head.Euler.X * 2));
            output.SetAxis(GamepadAxis.RightThumbX, ApplyDeadzone(-head.Euler.Y * 1f));
        }
        if (trackers.TryGetValue("HIP", out var hips))
        {
            output.SetAxis(GamepadAxis.LeftThumbX, ApplyDeadzone(hips.Euler.Y * 2));
            output.SetAxis(GamepadAxis.LeftThumbY, ApplyDeadzone(-hips.Euler.X * 2));
        }
        if (chest != null && trackers.TryGetValue("RIGHT_UPPER_ARM", out var rightHand))
        {
            output.SetButton(GamepadButton.Y, rightHand.Euler.Z + chest.Euler.Z < -30f);
            output.SetTrigger(GamepadTrigger.RightTrigger, (byte)(rightHand.Euler.Z - chest.Euler.Z > 30f ? 255 : 0));
        }
        if (chest != null && trackers.TryGetValue("LEFT_UPPER_ARM", out var leftHand))
        {
            output.SetTrigger(GamepadTrigger.LeftTrigger, (byte)(leftHand.Euler.Z - chest.Euler.Z < -30f ? 255 : 0));
        }
        if (trackers.TryGetValue("LEFT_FOOT", out var leftFoot))
        {
            output.SetButton(GamepadButton.LeftShoulder, leftFoot.Euler.X < -20);
        }
        if (trackers.TryGetValue("RIGHT_FOOT", out var rightFoot))
        {
            output.SetButton(GamepadButton.RightShoulder, rightFoot.Euler.X < -20);
        }
    }

    private static void FPS(Dictionary<string, TrackerState> trackers, IGamepadOutput output)
    {
        TrackerState? chest = null;
        trackers.TryGetValue("CHEST", out chest);

        if (trackers.TryGetValue("HEAD", out var head))
        {
            output.SetAxis(GamepadAxis.RightThumbY, ApplyDeadzone(-head.Euler.X * 2));
            output.SetAxis(GamepadAxis.RightThumbX, ApplyDeadzone(-head.Euler.Y * 1f));
        }
        if (trackers.TryGetValue("HIP", out var hips))
        {
            output.SetAxis(GamepadAxis.LeftThumbX, ApplyDeadzone(hips.Euler.Z * 2));
            output.SetAxis(GamepadAxis.LeftThumbY, ApplyDeadzone(-hips.Euler.X * 2));
        }
        if (chest != null && trackers.TryGetValue("RIGHT_UPPER_ARM", out var rightHand))
        {
            output.SetButton(GamepadButton.B, rightHand.Euler.Y - chest.Euler.Y > 30f);
            output.SetTrigger(GamepadTrigger.RightTrigger, (byte)(rightHand.Euler.Y + chest.Euler.Y < -15f ? 255 : 0));
        }
        if (chest != null && trackers.TryGetValue("LEFT_UPPER_ARM", out var leftHand))
        {
            output.SetTrigger(GamepadTrigger.LeftTrigger, (byte)(leftHand.Euler.Y - chest.Euler.Y < -20f ? 255 : 0));
        }

        if (trackers.TryGetValue("LEFT_FOOT", out var leftFoot))
        {
            output.SetButton(GamepadButton.A, leftFoot.Euler.X < -20);
            output.SetButton(GamepadButton.Y, leftFoot.Euler.Z > -20);
            output.SetButton(GamepadButton.LeftShoulder, leftFoot.Euler.Y > 5);
        }
        if (trackers.TryGetValue("RIGHT_FOOT", out var rightFoot))
        {
            output.SetButton(GamepadButton.A, rightFoot.Euler.X < -20);
            output.SetButton(GamepadButton.X, rightFoot.Euler.Z > 20);
            output.SetButton(GamepadButton.RightShoulder, rightFoot.Euler.Y < -5);
        }
    }

    private static void FFXIV(Dictionary<string, TrackerState> trackers, IGamepadOutput output)
    {
        if (trackers.TryGetValue("HIP", out var hips))
        {
            output.SetAxis(GamepadAxis.LeftThumbX, ApplyDeadzone(hips.Euler.Z * 1));
            output.SetAxis(GamepadAxis.LeftThumbY, ApplyDeadzone(-hips.Euler.X * 1));
        }

        TrackerState? leftFoot = null, rightFoot = null;
        if (trackers.TryGetValue("LEFT_FOOT", out leftFoot))
        {
            output.SetTrigger(GamepadTrigger.LeftTrigger, (byte)(leftFoot.Euler.Y > 15f ? 255 : 0));
        }
        if (trackers.TryGetValue("RIGHT_FOOT", out rightFoot))
        {
            output.SetTrigger(GamepadTrigger.RightTrigger, (byte)(rightFoot.Euler.Y < -15f ? 255 : 0));
        }

        if (leftFoot != null && rightFoot != null)
        {
            TrackingEnvironment.UpdateFloor(leftFoot, rightFoot);
        }

        if (trackers.TryGetValue("RIGHT_LOWER_ARM", out var rightHand))
        {
            output.SetButton(GamepadButton.Y, rightHand.FloorRelativePosition.Y > 0.1f);
            output.SetButton(GamepadButton.A, rightHand.FloorRelativePosition.Y < -0.02f);
            output.SetButton(GamepadButton.B, rightHand.FloorRelativePosition.Z > 0.1f);
            output.SetButton(GamepadButton.X, rightHand.FloorRelativePosition.Z < -0.1f);
        }
        if (trackers.TryGetValue("LEFT_UPPER_ARM", out var leftHand))
        {
            output.SetButton(GamepadButton.Up, leftHand.FloorRelativePosition.Y > 0.01f);
            output.SetButton(GamepadButton.Down, leftHand.FloorRelativePosition.Y < -0.02f);
            output.SetButton(GamepadButton.Left, leftHand.FloorRelativePosition.Z > 0.01f);
            output.SetButton(GamepadButton.Right, leftHand.FloorRelativePosition.Z < -0.01f);
        }
    }
}
