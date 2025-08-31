using System.Threading;
using System.Threading.Tasks;

namespace Netimobiledevice.Afc.Packets;

internal interface IAsyncAfcPacketVisitor
{
    ValueTask VisitAsync(AfcFileCloseRequest packet, CancellationToken cancellationToken = default);
    ValueTask VisitAsync(AfcFileInfoRequest packet, CancellationToken cancellationToken = default);
    ValueTask VisitAsync(AfcFileOpenRequest packet, CancellationToken cancellationToken = default);
    ValueTask VisitAsync(AfcFileReadRequest packet, CancellationToken cancellationToken = default);
    ValueTask VisitAsync(AfcFileWriteRequest packet, CancellationToken cancellationToken = default);
    ValueTask VisitAsync(AfcLockRequest packet, CancellationToken cancellationToken = default);
    ValueTask VisitAsync(AfcReadDirectoryRequest packet, CancellationToken cancellationToken = default);
    ValueTask VisitAsync(AfcRmRequest packet, CancellationToken cancellationToken = default);
    ValueTask VisitAsync(AfcSeekInfoRequest packet, CancellationToken cancellationToken = default);
    ValueTask VisitAsync(AfcTellRequest packet, CancellationToken cancellationToken = default);
}