namespace ImuToXInput.Core.Output;

/// <summary>
/// JOCP output (dongle → client) over TCP control channel (port 30101).
/// Header 12 bytes, then cmd_byte, then command-specific payload.
/// </summary>
public static class JocpOutputConstants
{
    public const ushort Magic = 0x4A50;  // "JP" little-endian
    public const byte MsgTypeOutputCmd = 0x04;

    public const byte CmdRumble = 0x01;
    public const byte CmdPlayerLed = 0x02;
    public const byte CmdRgbLed = 0x03;
    public const byte CmdPollRate = 0x04;

    public const int HeaderSize = 12;
    public const int RumblePayloadSize = 6;   // after cmd byte
    public const int PlayerLedPayloadSize = 1;
    public const int RgbLedPayloadSize = 3;
}

/// <summary>Parsed rumble command from dongle (JOCP_CMD_RUMBLE).</summary>
public sealed class JocpRumbleEventArgs : EventArgs
{
    public byte LeftAmplitude { get; }
    public byte LeftBrake { get; }
    public byte RightAmplitude { get; }
    public byte RightBrake { get; }
    public ushort DurationMs { get; }

    public JocpRumbleEventArgs(byte leftAmplitude, byte leftBrake, byte rightAmplitude, byte rightBrake, ushort durationMs)
    {
        LeftAmplitude = leftAmplitude;
        LeftBrake = leftBrake;
        RightAmplitude = rightAmplitude;
        RightBrake = rightBrake;
        DurationMs = durationMs;
    }
}
