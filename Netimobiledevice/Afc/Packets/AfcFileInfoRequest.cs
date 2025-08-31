using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Netimobiledevice.Afc.Packets;

internal record AfcFileInfoRequest(string Filename) : IAfcPacket
{
    public async ValueTask WritePacketToStreamAsync(Stream output, CancellationToken cancellationToken = default)
    {
        var fileName = Encoding.UTF8.GetBytes(Filename + '\0');
        var charCount = unchecked((ulong) fileName.Length);

        await output.WriteAsync(new AfcHeader(charCount, AfcOpCode.GetFileInfo), cancellationToken).ConfigureAwait(false);
        await output.WriteAsync(fileName, cancellationToken).ConfigureAwait(false);
    }
}
