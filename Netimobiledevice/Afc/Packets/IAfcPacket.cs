using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Netimobiledevice.Afc.Packets;

internal interface IAfcPacket
{
    ValueTask WritePacketToStreamAsync(Stream output, CancellationToken cancellationToken = default);
}