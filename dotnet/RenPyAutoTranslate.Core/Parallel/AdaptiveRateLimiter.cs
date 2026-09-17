using System.Diagnostics;

namespace RenPyAutoTranslate.Core.Parallel;

/// <summary>Shared minimum interval between translation attempts (all workers). Port of Python AdaptiveRateLimiter.</summary>
public sealed class AdaptiveRateLimiter
{
    private readonly double _ceiling;
    private readonly double _floor;
    private readonly double _firstBump;
    private readonly double _jitter;
    private readonly Action<string>? _onAdjust;
    private readonly SemaphoreSlim _mutex = new(1, 1);
    private double _nextAllowed;
    private double _interval;
    private static readonly Stopwatch Monotonic = Stopwatch.StartNew();

    private static double MonotonicNow() => Monotonic.Elapsed.TotalSeconds;

    public AdaptiveRateLimiter(
        double ceilingSec = 30.0,
        double firstBumpSec = 0.75,
        double minimumIntervalSec = 0.25,
        double jitterSec = 0.15,
        Action<string>? onAdjust = null)
    {
        _floor = Math.Max(0.05, minimumIntervalSec);
        _ceiling = Math.Max(_floor, ceilingSec);
        _firstBump = Math.Max(0.0, firstBumpSec);
        _jitter = Math.Max(0.0, jitterSec);
        _interval = _firstBump;
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

            // Positive jitter avoids synchronized, machine-like request timing without exceeding the rate cap.
            _nextAllowed = now + interval + Random.Shared.NextDouble() * _jitter;
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task RecordSuccessAsync(CancellationToken cancellationToken = default)
    {
        await _mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_interval <= 0)
                return;
            _interval = Math.Max(_floor, _interval * 0.995);
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task RecordThrottleAsync(CancellationToken cancellationToken = default)
    {
        await _mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var old = _interval;
            if (_interval <= 0)
                _interval = _firstBump;
            else
                _interval = Math.Min(_interval * 2.0, _ceiling);
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

    public void RecordSuccess() => RecordSuccessAsync().GetAwaiter().GetResult();

    public void RecordThrottle() => RecordThrottleAsync().GetAwaiter().GetResult();
}
