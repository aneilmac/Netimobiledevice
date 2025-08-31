using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Netimobiledevice.Afc.Packets;

namespace Netimobiledevice.Afc.Responses;

internal static class AfcResponseParser
{
    public static async ValueTask<ulong> ParseHeaderAsync(
        Stream stream,
        AfcOpCode expectedOpCode,
        CancellationToken cancellationToken = default)
    {
        var data = new byte[AfcHeader.HEADER_LENGTH];
        await stream.ReadExactlyAsync(data, cancellationToken).ConfigureAwait(false);
        using var memoryReader = new BinaryReader(new MemoryStream(data, false));
        AfcHeader header = memoryReader.ReadAfcHeader();
        if (header.Operation != expectedOpCode) {
            throw new AfcException(AfcError.OpHeaderInvalid, "Unexpected Op-header type returned");
        }
        return header.DataLength;
    }

    public static IReadOnlyList<string> ParseStringsList(ReadOnlySpan<byte> data)
    {
        var seperatedData = Encoding.UTF8.GetString(data).Split('\0');
        return new ArraySegment<string>(
            seperatedData,
            0,
        Math.Max(0, seperatedData.Length - 1));
    }
}