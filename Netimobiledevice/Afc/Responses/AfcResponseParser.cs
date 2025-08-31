using System;
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
        AfcHeader header = await stream.ReadAfcHeaderAsync(cancellationToken).ConfigureAwait(false);
        if (header.Operation != expectedOpCode) {
            throw new AfcException(AfcError.OpHeaderInvalid, "Unexpected Op-header type returned");
        }
        return header.DataLength;
    }

    public static ArraySegment<string> ParseStringsList(ReadOnlySpan<byte> data)
    {
        var seperatedData = Encoding.UTF8.GetString(data).Split('\0');
        return new ArraySegment<string>(
            seperatedData,
            0,
        Math.Max(0, seperatedData.Length - 1));
    }
}