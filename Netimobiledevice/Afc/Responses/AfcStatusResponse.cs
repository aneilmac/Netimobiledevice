using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
namespace Netimobiledevice.Afc.Responses;

internal record AfcStatusResponse(AfcError Error)
{
    public static async Task<AfcStatusResponse> ParseAsync(Stream input, CancellationToken cancellationToken)
    {
        var dataLength = await AfcResponseParser.ParseHeaderAsync(input, AfcOpCode.GetFileInfo, cancellationToken).ConfigureAwait(false);
        if (dataLength != sizeof(AfcError)) {
            throw new AfcException(AfcError.OpHeaderInvalid, "Unexpected Header size.");
        }
        using var reader = new BinaryReader(input, Encoding.UTF8, true);
        return new AfcStatusResponse((AfcError) reader.ReadUInt64());
    }

    public void ThrowIfNotSuccess()
    {
        if (Error != AfcError.Success) {
            throw new AfcException(Error);
        }
    }
}
