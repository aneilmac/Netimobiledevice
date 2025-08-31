using System.Buffers.Binary;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Netimobiledevice.Afc.Packets;

internal record AfcFileOpenRequest(AfcFileOpenMode Mode, string Filename)
{
    public async ValueTask WritePacketToStreamAsync(Stream output, CancellationToken cancellationToken = default)
    {
        var fileName = Encoding.UTF8.GetBytes(Filename + '\0');
        var charCount = unchecked((ulong) fileName.Length);
        await output.WriteAsync(new AfcHeader(sizeof(ulong) + charCount, AfcOpCode.FileOpen), cancellationToken).ConfigureAwait(false);

        var buffer = new byte[sizeof(ulong)];
        BinaryPrimitives.WriteUInt64LittleEndian(buffer, (ulong) Mode);
        await output.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
        await output.WriteAsync(fileName, cancellationToken).ConfigureAwait(false);
    }
}
