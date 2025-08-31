using System.IO;
using System.Threading;

namespace Netimobiledevice.Afc.Packets;

internal record AfcHeader(ulong Magic, ulong Length, ulong PacketNumber, AfcOpCode Operation)
{
    private static ulong _packetCounter = ulong.MaxValue;

    public const ulong HEADER_LENGTH = sizeof(ulong) * 4;

    // ASCII string "CFA6LPAA" in binary form
    public const ulong MAGIC = 4702127774209492547u;

    /// <summary>
    /// <see cref="Length"/> - <see cref="HEADER_LENGTH"/>
    /// </summary>
    public ulong DataLength => checked(Length - HEADER_LENGTH);

    internal AfcHeader(ulong DataLength, AfcOpCode Operation) :
        this(MAGIC, HEADER_LENGTH + DataLength, Interlocked.Increment(ref _packetCounter), Operation)
    {
    }
}

internal static class AfcHeaderExtensions
{
    public static void Write(this BinaryWriter @this, AfcHeader header)
    {
        @this.Write(header.Magic);
        @this.Write(header.Length);
        @this.Write(header.PacketNumber);
        @this.Write((ulong) header.Operation);
    }

    public static AfcHeader ReadAfcHeader(this BinaryReader @this)
    {
        ulong magic = @this.ReadUInt64();
        if (magic != AfcHeader.MAGIC) {
            throw new AfcException("Missmatch in magic bytes for afc header");
        }

        ulong length = @this.ReadUInt64();
        if (length < AfcHeader.HEADER_LENGTH) {
            throw new AfcException("Expected more bytes in afc header than received");
        }

        return new AfcHeader(
                magic, length,
            @this.ReadUInt64(),
            (AfcOpCode) @this.ReadUInt64());
    }
}