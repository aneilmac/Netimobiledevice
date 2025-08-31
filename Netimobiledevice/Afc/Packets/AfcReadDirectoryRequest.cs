using System.Threading;
using System.Threading.Tasks;

namespace Netimobiledevice.Afc.Packets;

internal record AfcReadDirectoryRequest(string Filename) : IAfcPacket
{
    public ValueTask AcceptAsync(IAsyncAfcPacketVisitor visitor, CancellationToken cancellationToken = default)
    => visitor.VisitAsync(this, cancellationToken);
}
