using System;
using System.Buffers.Binary;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Netimobiledevice.Afc.Packets;

internal record AfcFileWriteRequest(ulong Handle, ReadOnlyMemory<byte> Data)
{
    public async ValueTask WritePacketToStreamAsync(Stream output, CancellationToken cancellationToken = default)
    {
        await output.WriteAsync(new AfcHeader(sizeof(ulong) + unchecked((ulong) Data.Length), AfcOpCode.WriteFile), cancellationToken).ConfigureAwait(false);

        var buffer = new byte[sizeof(ulong)];
        BinaryPrimitives.WriteUInt64LittleEndian(buffer, Handle);
        await output.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);

        await output.WriteAsync(Data, cancellationToken).ConfigureAwait(false);
    }
}
