using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Netimobiledevice.Afc.Packets;

internal record AfcFileWriteRequest(ulong Handle, ReadOnlyMemory<byte> Data)
{
    public async IAsyncEnumerable<ulong> WritePacketToStreamAsync(
        Stream output,
        ulong chunkSize = 4096,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ulong dataLength = unchecked((ulong) Data.Length);
        ulong chunksCount = (dataLength / chunkSize) + ((dataLength % chunkSize == 0) ? 0ul : 1ul);

        var totalSize = (AfcHeader.HEADER_LENGTH + sizeof(ulong)) * chunksCount + (ulong) Data.Length;

        var handleBuffer = new byte[sizeof(ulong)];
        BinaryPrimitives.WriteUInt64LittleEndian(handleBuffer, Handle);

        for (ulong i = 0ul; i < dataLength; i += chunkSize) {
            cancellationToken.ThrowIfCancellationRequested();
            var truncatedChunkSize = Math.Min(chunkSize, dataLength - i);
            var chunk = Data.Slice((int) i, (int) truncatedChunkSize);
            var header = new AfcHeader(
                totalSize,
                AfcHeader.HEADER_LENGTH + sizeof(ulong) + truncatedChunkSize,
                AfcOpCode.WriteFile);
            await header.WriteAsync(output, cancellationToken).ConfigureAwait(false);
            await output.WriteAsync(handleBuffer, cancellationToken).ConfigureAwait(false);
            await output.WriteAsync(chunk, cancellationToken).ConfigureAwait(false);
            yield return i + truncatedChunkSize;
        }
    }
}
