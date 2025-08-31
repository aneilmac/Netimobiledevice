using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Netimobiledevice.Afc.Responses;

internal record AfcDelimitedStringResponse(ArraySegment<string> Strings)
{
    public static async ValueTask<AfcDelimitedStringResponse> ParseAsync(Stream input, AfcOpCode expectedOpCode, CancellationToken cancellationToken)
    {
        var dataLength = await AfcResponseParser.ParseHeaderAsync(input, expectedOpCode, cancellationToken).ConfigureAwait(false);
        var data = new byte[dataLength];
        await input.ReadExactlyAsync(data, cancellationToken).ConfigureAwait(false);
        return new AfcDelimitedStringResponse(AfcResponseParser.ParseStringsList(data));
    }
}
