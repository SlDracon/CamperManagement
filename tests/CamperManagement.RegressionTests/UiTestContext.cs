using System.Collections.Concurrent;
using System.Diagnostics;

// A deterministic UI event loop without a display server or Avalonia application startup.
sealed class UiTestContext : SynchronizationContext
{
    private readonly BlockingCollection<(SendOrPostCallback Callback, object? State)> _queue = new();
    public int ThreadId { get; } = Environment.CurrentManagedThreadId;

    public override void Post(SendOrPostCallback callback, object? state) => _queue.Add((callback, state));

    public void AssertCurrent()
    {
        if (Environment.CurrentManagedThreadId != ThreadId)
            throw new InvalidOperationException("UI/storage operation ran on a worker thread");
    }

    public static void Run(Func<UiTestContext, Task> action)
    {
        var previous = Current;
        var context = new UiTestContext();
        SetSynchronizationContext(context);
        try
        {
            var timeout = Stopwatch.StartNew();
            var task = action(context);
            while (!task.IsCompleted)
            {
                if (timeout.Elapsed > TimeSpan.FromSeconds(30))
                    throw new TimeoutException("UI responsiveness test did not complete");
                if (context._queue.TryTake(out var work, 100)) work.Callback(work.State);
            }
            task.GetAwaiter().GetResult();
            while (context._queue.TryTake(out var work)) work.Callback(work.State);
        }
        finally
        {
            SetSynchronizationContext(previous);
            context._queue.Dispose();
        }
    }
}

sealed class ResponsiveWriteStream(Stream inner, UiTestContext ui) : Stream
{
    private bool _checked;
    public override bool CanRead => inner.CanRead;
    public override bool CanSeek => inner.CanSeek;
    public override bool CanWrite => inner.CanWrite;
    public override long Length => inner.Length;
    public override long Position { get => inner.Position; set => inner.Position = value; }
    public override void Flush() => inner.Flush();
    public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
    public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);
    public override void SetLength(long value) => inner.SetLength(value);
    public override void Write(byte[] buffer, int offset, int count)
    {
        if (Environment.CurrentManagedThreadId == ui.ThreadId)
            throw new InvalidOperationException("PDF rendering/writing blocked the UI thread");
        if (!_checked)
        {
            _checked = true;
            // Keep this write blocked until the UI has processed an unrelated callback.
            var heartbeat = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            ui.Post(_ => heartbeat.SetResult(), null);
            if (!heartbeat.Task.Wait(TimeSpan.FromSeconds(5)))
                throw new TimeoutException("UI could not process a callback during PDF output");
        }
        inner.Write(buffer, offset, count);
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing) inner.Dispose();
        base.Dispose(disposing);
    }
}
