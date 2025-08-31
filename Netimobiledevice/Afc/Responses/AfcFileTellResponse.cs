using System.IO;
using System.Buffers.Binary;
using System;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace Netimobiledevice.Afc.Responses;

internal record AfcFileTellResponse(ulong Tell)
{
    public static async Task<AfcFileTellResponse> ParseAsync(Stream input, CancellationToken cancellationToken)
    {
        var dataLength = await AfcResponseParser.ParseHeaderAsync(input, AfcOpCode.FileTellResult, cancellationToken).ConfigureAwait(false);

        if (dataLength != sizeof(ulong)) {
            throw new AfcException(AfcError.OpHeaderInvalid, "Unexpected Header size.");
        }

        using var reader = new BinaryReader(input, Encoding.UTF8, true);
        var tell = reader.ReadUInt64();
        return new AfcFileTellResponse(
            BitConverter.IsLittleEndian
            ? BinaryPrimitives.ReverseEndianness(tell)
            : tell);
    }
}
