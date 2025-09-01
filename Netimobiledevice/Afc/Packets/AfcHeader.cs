using System;
using System.Buffers.Binary;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Netimobiledevice.Afc.Packets;

internal record AfcHeader(ulong Magic, ulong EntireLength, ulong ThisLength, ulong PacketNumber, AfcOpCode Operation)
{
    private static ulong _packetCounter = ulong.MaxValue;

    public const ulong HEADER_LENGTH = sizeof(ulong) * 5;

    // ASCII string "CFA6LPAA" in binary form
    public const ulong MAGIC = 4702127774209492547u;

    /// <summary>
    /// <see cref="ThisLength"/> - <see cref="HEADER_LENGTH"/>
    /// </summary>
    public ulong DataLength => checked(ThisLength - HEADER_LENGTH);

    internal AfcHeader(
        ulong EntireLength,
        ulong ThisLength,
        AfcOpCode Operation) :
        this(MAGIC,
        EntireLength,
        ThisLength,
        Interlocked.Increment(ref _packetCounter),
        Operation)
    {
    }

    internal AfcHeader(ulong DataLength, AfcOpCode Operation) :
        this(HEADER_LENGTH + DataLength, HEADER_LENGTH + DataLength, Operation)
    {
    }

    public void Write(Span<byte> dest)
    {
        BinaryPrimitives.WriteUInt64BigEndian(dest, Magic);
        BinaryPrimitives.WriteUInt64LittleEndian(dest[8..], EntireLength);
        BinaryPrimitives.WriteUInt64LittleEndian(dest[16..], ThisLength);
        BinaryPrimitives.WriteUInt64LittleEndian(dest[24..], PacketNumber);
        BinaryPrimitives.WriteUInt64LittleEndian(dest[32..], (ulong) Operation);
    }

    public ValueTask WriteAsync(Stream dest, CancellationToken cancellationToken)
    {
        var buffer = new byte[HEADER_LENGTH];
        Write(buffer);
        return dest.WriteAsync(buffer, cancellationToken);
    }

    public static AfcHeader Read(ReadOnlySpan<byte> source)
    {
        ulong magic = BinaryPrimitives.ReadUInt64BigEndian(source);

        if (magic != MAGIC) {
            throw new AfcException("Missmatch in magic bytes for afc header");
        }

        ulong entireLength = BinaryPrimitives.ReadUInt64LittleEndian(source[8..]);
        if (entireLength < HEADER_LENGTH) {
            throw new AfcException("Expected more bytes in afc header than received");
        }

        ulong thisLength = BinaryPrimitives.ReadUInt64LittleEndian(source[16..]);

        ulong packetNumber = BinaryPrimitives.ReadUInt64LittleEndian(source[24..]);
        ulong operation = BinaryPrimitives.ReadUInt64LittleEndian(source[32..]);

        return new AfcHeader(
            magic,
            entireLength,
            thisLength,
            packetNumber,
            (AfcOpCode) operation);
    }

    public static async ValueTask<AfcHeader> ReadAsync(Stream source, CancellationToken cancellationToken)
    {
        var buffer = new byte[HEADER_LENGTH];
        await source.ReadExactlyAsync(buffer, cancellationToken).ConfigureAwait(false);
        return Read(buffer);
    }
}
