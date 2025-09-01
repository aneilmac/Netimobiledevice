using System.Buffers.Binary;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Netimobiledevice.Afc.Packets;

internal record AfcLockRequest(ulong Handle, ulong Op)
{
    public async ValueTask WritePacketToStreamAsync(Stream output, CancellationToken cancellationToken = default)
    {
        await new AfcHeader(sizeof(ulong) * 2, AfcOpCode.FileLock)
            .WriteAsync(output, cancellationToken)
            .ConfigureAwait(false);

        var buffer = new byte[sizeof(ulong)];

        BinaryPrimitives.WriteUInt64LittleEndian(buffer, Handle);
        await output.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);

        BinaryPrimitives.WriteUInt64LittleEndian(buffer, Op);
        await output.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
    }
}
