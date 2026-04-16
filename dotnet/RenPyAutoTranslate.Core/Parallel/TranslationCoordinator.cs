using System.Net.Http;
using System.Threading.Tasks;
using RenPyAutoTranslate.Core.Renpy;

namespace RenPyAutoTranslate.Core.Parallel;

public readonly record struct RunResult(int Total, int Success, int Failed);

public readonly record struct TranslationProgress(int Completed, int Total, string LastRelativePath);

public sealed class TranslationCoordinator
{
    private readonly RenpyFileTranslator _translator;

    public TranslationCoordinator(RenpyFileTranslator translator)
    {
        _translator = translator;
    }

    public async Task<RunResult> RunAsync(
        IReadOnlyList<TranslationTask> tasks,
        string tlRoot,
        string outputRoot,
        int maxWorkers,
        IProgress<TranslationProgress>? progress,
        Action<string, string>? onLog,
        int maxRetries = 3,
        double baseDelaySec = 0.5,
        CancellationToken cancellationToken = default)
    {
        void Log(string level, string msg) => onLog?.Invoke(level, msg);

        if (tasks.Count == 0)
        {
            Log("INFO", "Translated: 0 / 0");
            Log("INFO", "Failed: 0 / 0");
            return new RunResult(0, 0, 0);
        }

        tlRoot = Path.GetFullPath(tlRoot);
        outputRoot = Path.GetFullPath(outputRoot);
        var workers = Math.Clamp(maxWorkers, 1, 32);
        var total = tasks.Count;
        var successCount = 0;
        var failCount = 0;
        var completed = 0;

        var limiter = new AdaptiveRateLimiter(onAdjust: msg => Log("INFO", msg));

        await global::System.Threading.Tasks.Parallel.ForEachAsync(
            tasks,
            new ParallelOptions { MaxDegreeOfParallelism = workers, CancellationToken = cancellationToken },
            async (task, ct) =>
            {
                try
                {
                    await TranslateFileWithRetryAsync(
                        task.Origin,
                        tlRoot,
                        outputRoot,
                        task.FromIso,
                        task.ToIso,
                        limiter,
                        maxRetries,
                        baseDelaySec,
                        ct).ConfigureAwait(false);
                    Interlocked.Increment(ref successCount);
                    var c = Interlocked.Increment(ref completed);
                    progress?.Report(new TranslationProgress(c, total, task.RelativePath));
                    Log("INFO", $"[{c}/{total}] OK {task.RelativePath.Replace('\\', '/')}");
                }
                catch (Exception exc)
                {
                    Interlocked.Increment(ref failCount);
                    var c = Interlocked.Increment(ref completed);
                    progress?.Report(new TranslationProgress(c, total, task.RelativePath));
                    Log("ERROR", $"[{c}/{total}] FAILED {task.RelativePath.Replace('\\', '/')}: {exc.Message}");
                }
            }).ConfigureAwait(false);

        Log("INFO", $"Translated: {successCount} / {total}");
        Log("INFO", $"Failed: {failCount} / {total}");
        return new RunResult(total, successCount, failCount);
    }

    private async Task TranslateFileWithRetryAsync(
        string origin,
        string tlRoot,
        string outputRoot,
        string fromL,
        string toL,
        AdaptiveRateLimiter limiter,
        int maxRetries,
        double baseDelaySec,
        CancellationToken ct)
    {
        Exception? last = null;
        for (var attempt = 0; attempt <= maxRetries; attempt++)
        {
            await limiter.AcquireAsync(ct).ConfigureAwait(false);
            try
            {
                await _translator
                    .TranslateFileAsync(origin, tlRoot, outputRoot, fromL, toL, ct)
                    .ConfigureAwait(false);
                limiter.RecordSuccess();
                return;
            }
            catch (Exception exc) when (exc is HttpRequestException or TaskCanceledException or IOException
                                        or System.Net.Http.HttpIOException)
            {
                last = exc;
            }
            catch (Exception exc)
            {
                if (TranslationErrorClassifier.IsRetriableException(exc))
                    last = exc;
                else
                    throw;
            }

            if (attempt < maxRetries)
            {
                if (last is not null && TranslationErrorClassifier.IsRateLimitError(last))
                    limiter.RecordThrottle();
                var delay = baseDelaySec * Math.Pow(2, attempt) + Random.Shared.NextDouble() * 0.25;
                await Task.Delay(TimeSpan.FromSeconds(delay), ct).ConfigureAwait(false);
            }
        }

        if (last is not null)
            throw last;
        throw new InvalidOperationException("translate_rpy_file_with_retry: exhausted retries");
    }
}
