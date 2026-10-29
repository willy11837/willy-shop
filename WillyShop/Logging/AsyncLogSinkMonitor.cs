using Serilog.Debugging;
using Serilog.Sinks.Async;

namespace WillyShop.Logging;

internal sealed class AsyncLogSinkMonitor : IAsyncLogEventSinkMonitor
{
    private const int WarningUsagePercentage = 80;
    private readonly object _sync = new();
    private Timer? _timer;
    private long _lastDroppedMessagesCount;
    private bool _bufferPressureReported;

    public void StartMonitoring(IAsyncLogEventSinkInspector inspector)
    {
        lock (_sync)
        {
            _timer?.Dispose();
            _timer = new Timer(
                _ => Inspect(inspector),
                null,
                TimeSpan.FromSeconds(30),
                TimeSpan.FromSeconds(30));
        }
    }

    public void StopMonitoring(IAsyncLogEventSinkInspector inspector)
    {
        lock (_sync)
        {
            _timer?.Dispose();
            _timer = null;
        }
    }

    private void Inspect(IAsyncLogEventSinkInspector inspector)
    {
        lock (_sync)
        {
            try
            {
                var usagePercentage = inspector.BufferSize == 0
                    ? 0
                    : inspector.Count * 100 / inspector.BufferSize;
                if (usagePercentage >= WarningUsagePercentage && !_bufferPressureReported)
                {
                    SelfLog.WriteLine(
                        "WillyShop async log buffer usage is {0}% ({1}/{2}).",
                        usagePercentage,
                        inspector.Count,
                        inspector.BufferSize);
                    _bufferPressureReported = true;
                }
                else if (usagePercentage < WarningUsagePercentage / 2)
                {
                    _bufferPressureReported = false;
                }

                var droppedMessagesCount = inspector.DroppedMessagesCount;
                if (droppedMessagesCount > _lastDroppedMessagesCount)
                {
                    SelfLog.WriteLine(
                        "WillyShop async log sink dropped {0} additional events; total dropped: {1}.",
                        droppedMessagesCount - _lastDroppedMessagesCount,
                        droppedMessagesCount);
                    _lastDroppedMessagesCount = droppedMessagesCount;
                }
            }
            catch (ObjectDisposedException)
            {
                // The sink can be disposed concurrently during application shutdown.
            }
        }
    }
}