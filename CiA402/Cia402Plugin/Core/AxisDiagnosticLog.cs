using System.Threading.Channels;

namespace CiA402.Core;

/// <summary>有界、保序的轴诊断。宿主日志阻塞时最多占用一个后台工作线程。</summary>
internal sealed class AxisDiagnosticLog(Action<string, Exception?> write)
{
    private readonly Channel<(string Message, Exception? Error)> _messages = Channel.CreateBounded<(string, Exception?)>(
        new BoundedChannelOptions(16) { FullMode = BoundedChannelFullMode.DropWrite, SingleReader = true });
    private int _draining;

    public void Report(string message, Exception? error)
    {
        _messages.Writer.TryWrite((message, error));
        Schedule();
    }

    private void Schedule()
    {
        if (Interlocked.CompareExchange(ref _draining, 1, 0) == 0) _ = Task.Run(Drain);
    }

    private void Drain()
    {
        try
        {
            while (_messages.Reader.TryRead(out var entry))
                try { write(entry.Message, entry.Error); } catch (Exception) { }
        }
        finally
        {
            Volatile.Write(ref _draining, 0);
            if (_messages.Reader.TryPeek(out _)) Schedule();
        }
    }
}
