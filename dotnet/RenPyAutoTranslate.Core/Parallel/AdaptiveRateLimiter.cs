using System.Diagnostics;

namespace RenPyAutoTranslate.Core.Parallel;

/// <summary>Shared minimum interval between translation attempts (all workers). Port of Python AdaptiveRateLimiter.</summary>
public sealed class AdaptiveRateLimiter
{
    private readonly double _ceiling;
    private readonly double _firstBump;
    private readonly Action<string>? _onAdjust;
    private readonly SemaphoreSlim _mutex = new(1, 1);
    private double _nextAllowed;
    private double _interval;
    private static readonly Stopwatch Monotonic = Stopwatch.StartNew();

    private static double MonotonicNow() => Monotonic.Elapsed.TotalSeconds;

    public AdaptiveRateLimiter(
        double ceilingSec = 8.0,
        double firstBumpSec = 0.05,
        Action<string>? onAdjust = null)
    {
        _ceiling = Math.Max(0.05, ceilingSec);
        _firstBump = Math.Max(0.0, firstBumpSec);
        _onAdjust = onAdjust;
    }

    public async Task AcquireAsync(CancellationToken cancellationToken = default)
    {
        await _mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var interval = _interval;
            var now = MonotonicNow();
            if (now < _nextAllowed)
            {
                var delay = TimeSpan.FromSeconds(_nextAllowed - now);
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                now = MonotonicNow();
            }

            _nextAllowed = now + interval;
        }
        finally
        {
            _mutex.Release();
        }
    }

    public void RecordSuccess()
    {
        _mutex.Wait();
        try
        {
            if (_interval <= 0)
                return;
            _interval = Math.Max(0.0, _interval * 0.992);
            if (_interval < 0.001)
                _interval = 0.0;
        }
        finally
        {
            _mutex.Release();
        }
    }

    public void RecordThrottle()
    {
        _mutex.Wait();
        try
        {
            var old = _interval;
            if (_interval <= 0)
                _interval = _firstBump;
            else
                _interval = Math.Min(_interval * 1.85, _ceiling);
            if (_interval > old && _onAdjust is not null)
            {
                var ms = _interval * 1000.0;
                _onAdjust($"Server throttling detected; spacing requests ~{ms:0}ms apart");
            }
        }
        finally
        {
            _mutex.Release();
        }
    }
}
