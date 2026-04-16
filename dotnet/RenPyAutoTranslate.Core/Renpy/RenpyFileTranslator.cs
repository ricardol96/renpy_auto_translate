using System.Text;
using RenPyAutoTranslate.Core.Translation;

namespace RenPyAutoTranslate.Core.Renpy;

public sealed class RenpyFileTranslator
{
    private readonly ITranslationProvider _provider;

    public RenpyFileTranslator(ITranslationProvider provider)
    {
        _provider = provider;
    }

    /// <summary>
    /// Translates one .rpy. Writes only to a temp file, then moves into place.
    /// Any translation failure (HTTP, rate limit, bad API response) or line merge failure throws;
    /// the temp file is removed and the final output path is not created or updated.
    /// Lines after a translatable comment/old line that contain no ASCII double quote (e.g. <c>voice</c>) are copied as-is until a dialogue line with <c>"..."</c> is found.
    /// </summary>
    public async Task TranslateFileAsync(
        string originFile,
        string tlRoot,
        string outputRoot,
        string fromL,
        string toL,
        CancellationToken cancellationToken = default)
    {
        tlRoot = Path.GetFullPath(tlRoot);
        outputRoot = Path.GetFullPath(outputRoot);
        var rel = Path.GetRelativePath(tlRoot, originFile);
        var outPath = Path.Combine(outputRoot, rel);
        var tmpPath = outPath + ".tmp";
        var outDir = Path.GetDirectoryName(outPath);
        if (!string.IsNullOrEmpty(outDir))
            Directory.CreateDirectory(outDir);

        try
        {
            await using (var input = File.OpenRead(originFile))
            using (var reader = new StreamReader(input, Encoding.UTF8))
            await using (var output = File.Create(tmpPath))
            using (var writer = new StreamWriter(output, new UTF8Encoding(false)))
            {
                Pending? pending = null;
                string? line;
                var lineNumber = 0;
                while ((line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false)) is not null)
                {
                    lineNumber++;
                    if (pending is not null)
                    {
                        string? filled;
                        if (pending.Kind == PendingKind.Single)
                        {
                            filled = RenpyStringUtils.ReplaceFirstQuotedBody(line, pending.Single!);
                        }
                        else
                        {
                            filled = RenpyStringUtils.FillNewDoubleSpeakerLine(
                                line, pending.Speaker!, pending.Dialogue!);
                        }

                        if (filled is null)
                        {
                            // Interstitial lines (voice, scene, show, etc.): no dialogue "..." here — copy verbatim (not sent to the API) and keep pending for the next quoted line.
                            if (line.IndexOf('"', StringComparison.Ordinal) < 0)
                            {
                                await writer.WriteLineAsync(line).ConfigureAwait(false);
                                continue;
                            }

                            var preview = line.TrimEnd('\r', '\n');
                            if (preview.Length > 160)
                                preview = preview[..160] + "…";
                            throw new RenpyLineFillException(
                                $"Could not apply translation to dialogue line {lineNumber} in {originFile}. " +
                                "Expected a replaceable ASCII \"...\" string (or double-speaker \"\" slot) after the previous translatable line. " +
                                $"Next line: {preview}");
                        }

                        await writer.WriteLineAsync(filled).ConfigureAwait(false);
                        pending = null;
                        continue;
                    }

                    var pair = RenpyStringUtils.ExtractSpeakerAndDialogueFromHashComment(line);
                    if (pair is not null)
                    {
                        await writer.WriteLineAsync(line).ConfigureAwait(false);
                        var spk = await TranslateWithBracketProtectionAsync(
                            pair.Value.Speaker, fromL, toL, cancellationToken).ConfigureAwait(false);
                        var dlg = await TranslateWithBracketProtectionAsync(
                            pair.Value.Dialogue, fromL, toL, cancellationToken).ConfigureAwait(false);
                        pending = new Pending(PendingKind.Double, null, spk, dlg);
                        continue;
                    }

                    var lineToTranslate = RenpyStringUtils.GetStringToTranslate(line);
                    if (lineToTranslate is not null)
                    {
                        await writer.WriteLineAsync(line).ConfigureAwait(false);
                        var tr = await TranslateWithBracketProtectionAsync(
                            lineToTranslate, fromL, toL, cancellationToken).ConfigureAwait(false);
                        pending = new Pending(PendingKind.Single, tr, null, null);
                        continue;
                    }

                    await writer.WriteLineAsync(line).ConfigureAwait(false);
                }

                if (pending is not null)
                {
                    throw new RenpyLineFillException(
                        $"File ended before the dialogue line following a translatable line in {originFile}.");
                }
            }

            File.Move(tmpPath, outPath, overwrite: true);
        }
        catch
        {
            if (File.Exists(tmpPath))
            {
                try { File.Delete(tmpPath); } catch { /* ignore */ }
            }

            throw;
        }
    }

    private async Task<string> TranslateWithBracketProtectionAsync(
        string text,
        string fromL,
        string toL,
        CancellationToken cancellationToken)
    {
        var masked = RenpyInterpolationProtector.MaskBracketInterpolations(text, out var originals);
        var translated = await _provider
            .TranslateAsync(masked, fromL, toL, cancellationToken)
            .ConfigureAwait(false);
        return RenpyInterpolationProtector.UnmaskBracketInterpolations(translated, originals);
    }

    private sealed class Pending
    {
        public PendingKind Kind { get; }
        public string? Single { get; }
        public string? Speaker { get; }
        public string? Dialogue { get; }

        public Pending(PendingKind kind, string? single, string? speaker, string? dialogue)
        {
            Kind = kind;
            Single = single;
            Speaker = speaker;
            Dialogue = dialogue;
        }
    }

    private enum PendingKind
    {
        Single,
        Double
    }
}
