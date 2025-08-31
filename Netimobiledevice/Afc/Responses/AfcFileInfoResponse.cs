using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Netimobiledevice.Afc.Responses;

internal record AfcFileInfoResponse(IReadOnlyDictionary<string, string> Info)
{
    public static async ValueTask<AfcFileInfoResponse> ParseAsync(Stream input, CancellationToken cancellationToken)
    {
        var dataLength = await AfcResponseParser.ParseHeaderAsync(
            input,
            AfcOpCode.GetFileInfo,
            cancellationToken)
            .ConfigureAwait(false);

        var data = new byte[dataLength];
        await input.ReadExactlyAsync(data, cancellationToken)
            .ConfigureAwait(false);
        var seperatedData = AfcResponseParser.ParseStringsList(data);
        if (seperatedData.Count % 2 != 0) {
            throw new AfcException("Received data not balanced, unable to parse to dictionary");
        }

        var dict = ImmutableDictionary.CreateBuilder<string, string>();
        for (int i = 0; i < seperatedData.Count; i += 2) {
            dict.Add(seperatedData[i], seperatedData[i + 1]);
        }
        return new AfcFileInfoResponse(dict.ToDictionary());
    }
}
