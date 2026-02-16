namespace ImuToXInput.Core.Output;

/// <summary>
/// Holds current gamepad state and serializes to the 12-byte Xbox 360 report format
/// used for BLE to the nRF dongle. Layout matches ImuToXInput-ESP32 Xbox360Report.
/// </summary>
public sealed class GamepadReportState
{
    public short AxisLeftX { get; set; }
    public short AxisLeftY { get; set; }
    public short AxisRightX { get; set; }
    public short AxisRightY { get; set; }
    public ushort Buttons { get; set; }
    public byte TriggerLeft { get; set; }
    public byte TriggerRight { get; set; }

    public const int ReportSize = 12;

    public void SetAxis(GamepadAxis axis, short value)
    {
        switch (axis)
        {
            case GamepadAxis.LeftThumbX: AxisLeftX = value; break;
            case GamepadAxis.LeftThumbY: AxisLeftY = value; break;
            case GamepadAxis.RightThumbX: AxisRightX = value; break;
            case GamepadAxis.RightThumbY: AxisRightY = value; break;
        }
    }

    public void SetButton(GamepadButton button, bool pressed)
    {
        ushort bit = button switch
        {
            GamepadButton.A => 1 << 0,
            GamepadButton.B => 1 << 1,
            GamepadButton.X => 1 << 2,
            GamepadButton.Y => 1 << 3,
            GamepadButton.LeftShoulder => 1 << 4,
            GamepadButton.RightShoulder => 1 << 5,
            GamepadButton.Back => 1 << 6,
            GamepadButton.Start => 1 << 7,
            GamepadButton.Up => 1 << 8,
            GamepadButton.Down => 1 << 9,
            GamepadButton.Left => 1 << 10,
            GamepadButton.Right => 1 << 11,
            _ => 0
        };
        if (pressed)
        {
            Buttons |= bit;
        }
        else
        {
            Buttons = (ushort)(Buttons & ~bit);
        }
    }

    public void SetTrigger(GamepadTrigger trigger, byte value)
    {
        if (trigger == GamepadTrigger.LeftTrigger)
        {
            TriggerLeft = value;
        }
        else
        {
            TriggerRight = value;
        }
    }

    /// <summary>Writes the 12-byte report (little-endian) to the buffer. Returns ReportSize.</summary>
    public int WriteTo(Span<byte> buffer)
    {
        if (buffer.Length < ReportSize)
        {
            return 0;
        }
        WriteLittleEndian(buffer, 0, AxisLeftX);
        WriteLittleEndian(buffer, 2, AxisLeftY);
        WriteLittleEndian(buffer, 4, AxisRightX);
        WriteLittleEndian(buffer, 6, AxisRightY);
        WriteLittleEndian(buffer, 8, (ushort)Buttons);
        buffer[10] = TriggerLeft;
        buffer[11] = TriggerRight;
        return ReportSize;
    }

    public byte[] ToBytes()
    {
        var bytes = new byte[ReportSize];
        WriteTo(bytes);
        return bytes;
    }

    private static void WriteLittleEndian(Span<byte> buf, int offset, short value)
    {
        buf[offset] = (byte)value;
        buf[offset + 1] = (byte)(value >> 8);
    }

    private static void WriteLittleEndian(Span<byte> buf, int offset, ushort value)
    {
        buf[offset] = (byte)value;
        buf[offset + 1] = (byte)(value >> 8);
    }
}
