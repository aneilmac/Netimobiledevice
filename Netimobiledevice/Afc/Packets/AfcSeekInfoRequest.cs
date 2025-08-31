using System.Threading;
using System.Threading.Tasks;

namespace Netimobiledevice.Afc.Packets;

internal record AfcSeekInfoRequest(ulong Handle, ulong Whence, long Offset) : IAfcPacket
{
    public ValueTask AcceptAsync(IAsyncAfcPacketVisitor visitor, CancellationToken cancellationToken = default)
   => visitor.VisitAsync(this, cancellationToken);
}
