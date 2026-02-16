namespace ImuToXInput.Core.Output;

/// <summary>
/// Builds Joypad Open Controller Protocol (JOCP) INPUT packets for Joypad OS WiFi.
/// Packet: 12-byte header + 64-byte payload = 76 bytes, sent to UDP port 30100.
/// </summary>
public static class JocpPacket
{
    public const int HeaderSize = 12;
    public const int PayloadSize = 64;
    public const int PacketSize = HeaderSize + PayloadSize;

    private const ushort Magic = 0x4A50;   // 'JP'
    private const byte Version = 0x01;
    private const byte MsgTypeInput = 0x01;

    /// <summary>Builds a 76-byte JOCP INPUT packet from the 12-byte Xbox 360-style report (same as BLE dongle).</summary>
    /// <param name="report12">Exactly 12 bytes: axisLeftX, axisLeftY, axisRightX, axisRightY (2 each), buttons (2), triggerLeft, triggerRight (1 each). Little-endian.</param>
    /// <param name="sequence">Incrementing sequence number (optional; pass 0 to omit).</param>
    /// <param name="timestampMs">Timestamp in ms (optional; pass 0 to omit).</param>
    /// <returns>76-byte buffer suitable for UDP send.</returns>
    public static byte[] BuildFromXbox360Report(ReadOnlySpan<byte> report12, ushort sequence = 0, uint timestampMs = 0)
    {
        if (report12.Length < 12)
        {
            return new byte[PacketSize];
        }

        var packet = new byte[PacketSize];

        // Header (12 bytes): magic, version, msg_type, seq, flags, timestamp
        packet[0] = (byte)(Magic & 0xFF);
        packet[1] = (byte)(Magic >> 8);
        packet[2] = Version;
        packet[3] = MsgTypeInput;
        packet[4] = (byte)(sequence & 0xFF);
        packet[5] = (byte)(sequence >> 8);
        packet[6] = 0;
        packet[7] = 0;
        packet[8] = (byte)(timestampMs & 0xFF);
        packet[9] = (byte)(timestampMs >> 8);
        packet[10] = (byte)(timestampMs >> 16);
        packet[11] = (byte)(timestampMs >> 24);

        // Payload (64 bytes): buttons (4), lx, ly, rx, ry (2 each), lt, rt (2 each), rest zero
        uint buttons = report12[8] | ((uint)report12[9] << 8);
        int offset = HeaderSize;
        packet[offset] = (byte)(buttons & 0xFF);
        packet[offset + 1] = (byte)(buttons >> 8);
        packet[offset + 2] = (byte)(buttons >> 16);
        packet[offset + 3] = (byte)(buttons >> 24);
        offset += 4;

        // Sticks (int16 little-endian)
        packet[offset] = report12[0];
        packet[offset + 1] = report12[1];
        packet[offset + 2] = report12[2];
        packet[offset + 3] = report12[3];
        packet[offset + 4] = report12[4];
        packet[offset + 5] = report12[5];
        packet[offset + 6] = report12[6];
        packet[offset + 7] = report12[7];
        offset += 8;

        // Triggers: 0..255 -> uint16 (0..65535)
        ushort lt = (ushort)((report12[10] << 8) | report12[10]);
        ushort rt = (ushort)((report12[11] << 8) | report12[11]);
        packet[offset] = (byte)(lt & 0xFF);
        packet[offset + 1] = (byte)(lt >> 8);
        packet[offset + 2] = (byte)(rt & 0xFF);
        packet[offset + 3] = (byte)(rt >> 8);
        // offset += 4; rest of payload (64 - 16 = 48 bytes) already zero

        return packet;
    }
}
