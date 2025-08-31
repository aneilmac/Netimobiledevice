using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Netimobiledevice.Afc.Responses;

internal record AfcFileReadResponse(int DataRead)
{
    public static async ValueTask<AfcFileReadResponse> ParseAsync(Stream input, Memory<byte> dest, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var dataLength = await AfcResponseParser.ParseHeaderAsync(input, AfcOpCode.ReadFile, cancellationToken).ConfigureAwait(false);
        var dataToRead = checked((int) dataLength);
        await input.ReadExactlyAsync(dest[..dataToRead], cancellationToken).ConfigureAwait(false);
        return new AfcFileReadResponse(dataToRead);
    }
}
