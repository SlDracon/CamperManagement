using System;
using System.IO;
using System.Threading;
namespace CamperManagement.Services;
internal sealed class CancellationWriteStream(Stream inner, CancellationToken token) : Stream
{
    public override bool CanRead => false;
    public override bool CanSeek => inner.CanSeek;
    public override bool CanWrite => inner.CanWrite;
    public override long Length => inner.Length;
    public override long Position { get => inner.Position; set => inner.Position = value; }
    public override void Flush() { token.ThrowIfCancellationRequested(); inner.Flush(); }
    public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);
    public override void SetLength(long value) { token.ThrowIfCancellationRequested(); inner.SetLength(value); }
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) { token.ThrowIfCancellationRequested(); inner.Write(buffer, offset, count); }
    public override void Write(ReadOnlySpan<byte> buffer) { token.ThrowIfCancellationRequested(); inner.Write(buffer); }
    // The platform stream is owned and asynchronously disposed by WriteFileAsync.
}
