using System.Buffers.Binary;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

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
    public static async ValueTask WriteAsync(this Stream @this, AfcHeader header, CancellationToken cancellationToken)
    {
        var buffer = new byte[sizeof(ulong)];
        BinaryPrimitives.WriteUInt64BigEndian(buffer, header.Magic);
        await @this.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);

        BinaryPrimitives.WriteUInt64LittleEndian(buffer, header.Length);
        await @this.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);

        BinaryPrimitives.WriteUInt64LittleEndian(buffer, header.PacketNumber);
        await @this.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);

        BinaryPrimitives.WriteUInt64LittleEndian(buffer, (ulong) header.Operation);
        await @this.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
    }

    public static async ValueTask<AfcHeader> ReadAfcHeaderAsync(this Stream @this, CancellationToken cancellationToken)
    {
        var buffer = new byte[sizeof(ulong)];

        await @this.ReadExactlyAsync(buffer, cancellationToken).ConfigureAwait(false);
        ulong magic = BinaryPrimitives.ReadUInt64BigEndian(buffer);

        if (magic != AfcHeader.MAGIC) {
            throw new AfcException("Missmatch in magic bytes for afc header");
        }

        await @this.ReadExactlyAsync(buffer, cancellationToken).ConfigureAwait(false);
        ulong length = BinaryPrimitives.ReadUInt64LittleEndian(buffer);
        if (length < AfcHeader.HEADER_LENGTH) {
            throw new AfcException("Expected more bytes in afc header than received");
        }

        await @this.ReadExactlyAsync(buffer, cancellationToken).ConfigureAwait(false);
        ulong packetNumber = BinaryPrimitives.ReadUInt64LittleEndian(buffer);

        await @this.ReadExactlyAsync(buffer, cancellationToken).ConfigureAwait(false);
        ulong operation = BinaryPrimitives.ReadUInt64LittleEndian(buffer);

        return new AfcHeader(magic, length, packetNumber, (AfcOpCode) operation);
    }
}