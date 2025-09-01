using System.Buffers.Binary;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Netimobiledevice.Afc.Packets;

internal record AfcFileCloseRequest(ulong Handle)
{
    public async ValueTask WritePacketToStreamAsync(Stream output, CancellationToken cancellationToken = default)
    {
        await new AfcHeader(sizeof(ulong), AfcOpCode.FileClose)
            .WriteAsync(output, cancellationToken)
                .ConfigureAwait(false);

        var buffer = new byte[sizeof(ulong)];
        BinaryPrimitives.WriteUInt64LittleEndian(buffer, Handle);
        await output.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
    }
}
