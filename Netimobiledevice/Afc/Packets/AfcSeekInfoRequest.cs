using System.Buffers.Binary;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Netimobiledevice.Afc.Packets;

internal record AfcSeekInfoRequest(ulong Handle, ulong Whence, long Offset)
{
    public async ValueTask WritePacketToStreamAsync(Stream output, CancellationToken cancellationToken = default)
    {
        await new AfcHeader((sizeof(ulong) * 2) + sizeof(long), AfcOpCode.FileSeek)
            .WriteAsync(output, cancellationToken)
            .ConfigureAwait(false);

        var buffer = new byte[sizeof(ulong)];

        BinaryPrimitives.WriteUInt64LittleEndian(buffer, Handle);
        await output.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);

        BinaryPrimitives.WriteUInt64LittleEndian(buffer, Whence);
        await output.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);

        BinaryPrimitives.WriteInt64LittleEndian(buffer, Offset);
        await output.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
    }
}
