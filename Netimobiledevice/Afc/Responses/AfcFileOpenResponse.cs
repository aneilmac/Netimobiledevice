using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Netimobiledevice.Afc.Responses;


internal record AfcFileOpenResponse(ulong Handle)
{
    public static async ValueTask<AfcFileOpenResponse> ParseAsync(Stream input, CancellationToken cancellationToken)
    {
        var dataLength = await AfcResponseParser.ParseHeaderAsync(
            input,
            AfcOpCode.FileOpenResult,
            cancellationToken).ConfigureAwait(false);

        if (dataLength != sizeof(ulong)) {
            throw new AfcException(AfcError.OpHeaderInvalid, "Unexpected Header size.");
        }

        using var reader = new BinaryReader(input, Encoding.UTF8, true);
        return new AfcFileOpenResponse(reader.ReadUInt64());
    }
}
