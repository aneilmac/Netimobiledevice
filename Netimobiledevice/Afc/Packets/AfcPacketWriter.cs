using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Netimobiledevice.Afc.Packets;

internal sealed class AfcPacketWriter : IAsyncAfcPacketVisitor, IDisposable, IAsyncDisposable
{
    private readonly Stream _stream;
    private readonly BinaryWriter _writer;

    public void Dispose() => _writer.Dispose();

    public ValueTask DisposeAsync() => _writer.DisposeAsync();

    public AfcPacketWriter(Stream stream, bool leaveOpen = false)
    {
        _stream = stream;
        _writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen);
    }

    public ValueTask VisitAsync(AfcFileCloseRequest packet, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _writer.Write(new AfcHeader(sizeof(ulong), AfcOpCode.FileClose));
        _writer.Write(packet.Handle);
        return ValueTask.CompletedTask;
    }

    public ValueTask VisitAsync(AfcFileInfoRequest packet, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _writer.Write(new AfcHeader(CharCount(packet.Filename), AfcOpCode.GetFileInfo));
        _writer.Write(packet.Filename);
        _writer.Write((byte) 0);
        return ValueTask.CompletedTask;
    }

    public ValueTask VisitAsync(AfcFileOpenRequest packet, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var charCount = CharCount(packet.Filename);
        _writer.Write(new AfcHeader(sizeof(ulong) + charCount, AfcOpCode.FileOpen));
        _writer.Write((ulong) packet.Mode);
        _writer.Write(packet.Filename);
        _writer.Write((byte) 0);
        return ValueTask.CompletedTask;
    }

    public ValueTask VisitAsync(AfcFileReadRequest packet, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _writer.Write(new AfcHeader(sizeof(ulong) * 2, AfcOpCode.ReadFile));
        _writer.Write(packet.Handle);
        _writer.Write(packet.Size);
        return ValueTask.CompletedTask;
    }

    public async ValueTask VisitAsync(AfcFileWriteRequest packet, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _writer.Write(new AfcHeader(sizeof(ulong) + unchecked((ulong) packet.Data.Length), AfcOpCode.WriteFile));
        _writer.Write(packet.Handle);
        await _stream.WriteAsync(packet.Data, cancellationToken).ConfigureAwait(false);
    }

    public ValueTask VisitAsync(AfcLockRequest packet, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _writer.Write(new AfcHeader(sizeof(ulong) * 2, AfcOpCode.FileLock));
        _writer.Write(packet.Handle);
        _writer.Write(packet.Op);
        return ValueTask.CompletedTask;
    }

    public ValueTask VisitAsync(AfcReadDirectoryRequest packet, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _writer.Write(new AfcHeader(CharCount(packet.Filename), AfcOpCode.ReadDir));
        _writer.Write(packet.Filename);
        _writer.Write((byte) 0);
        return ValueTask.CompletedTask;
    }

    public ValueTask VisitAsync(AfcRmRequest packet, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _writer.Write(new AfcHeader(CharCount(packet.Filename), AfcOpCode.RemovePath));
        _writer.Write(packet.Filename);
        _writer.Write((byte) 0);
        return ValueTask.CompletedTask;
    }

    public ValueTask VisitAsync(AfcSeekInfoRequest packet, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _writer.Write(new AfcHeader((sizeof(ulong) * 2) + sizeof(long), AfcOpCode.FileSeek));
        _writer.Write(packet.Handle);
        _writer.Write(packet.Whence);
        _writer.Write(packet.Offset);
        return ValueTask.CompletedTask;
    }

    public ValueTask VisitAsync(AfcTellRequest packet, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _writer.Write(new AfcHeader(sizeof(ulong), AfcOpCode.FileTell));
        _writer.Write(packet.Handle);
        return ValueTask.CompletedTask;
    }

    private static ulong CharCount(string s) => unchecked((ulong) (Encoding.UTF8.GetByteCount(s) + 1));
}