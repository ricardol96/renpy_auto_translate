using RenPyAutoTranslate.Core.Paths;

namespace RenPyAutoTranslate.Core.Renpy;

public static class TlDiscovery
{
    public static IReadOnlyList<string> CollectRpyPathsUnder(string root)
    {
        root = Path.GetFullPath(root);
        var outList = new List<string>();
        if (!Directory.Exists(root))
            return outList;
        foreach (var path in Directory.EnumerateFiles(root, "*.rpy", SearchOption.AllDirectories))
            outList.Add(path);
        return outList;
    }

    public static bool RpyHasUnfilledEmptyNewStrings(string path)
    {
        try
        {
            var data = File.ReadAllText(path);
            return RenpyStringUtils.RpyHasUnfilledEmptyNewStrings(data);
        }
        catch (IOException)
        {
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return true;
        }
    }

    public static IReadOnlyList<string> ListMissingRpyFiles(string tlRoot, string outputRoot, string langFolder)
    {
        tlRoot = Path.GetFullPath(tlRoot);
        outputRoot = Path.GetFullPath(outputRoot);
        var inPlace = RenpyPaths.PathsEqualCaseInsensitive(tlRoot, outputRoot);
        var langDir = Path.Combine(tlRoot, langFolder);
        var missing = new List<string>();
        foreach (var origin in CollectRpyPathsUnder(langDir))
        {
            var rel = Path.GetRelativePath(tlRoot, origin);
            var outPath = Path.Combine(outputRoot, rel);
            if (!File.Exists(outPath) || new FileInfo(outPath).Length == 0)
            {
                missing.Add(origin);
                continue;
            }

            if (inPlace && RpyHasUnfilledEmptyNewStrings(origin))
                missing.Add(origin);
        }

        return missing;
    }

    public static IReadOnlyList<TranslationTask> BuildTasks(
        IReadOnlyList<string> origins,
        string tlRoot,
        string sourceIso,
        string targetIso)
    {
        tlRoot = Path.GetFullPath(tlRoot);
        var tasks = new List<TranslationTask>(origins.Count);
        foreach (var origin in origins)
        {
            var rel = Path.GetRelativePath(tlRoot, origin);
            var baseName = Path.GetFileName(origin);
            var fromL = string.Equals(baseName, "common.rpy", StringComparison.OrdinalIgnoreCase)
                ? "en"
                : sourceIso;
            var toL = targetIso;
            tasks.Add(new TranslationTask(origin, rel, fromL, toL));
        }

        return tasks;
    }

    /// <summary>Mirror directory structure without files (Ren'Py empty TL tree).</summary>
    public static void CopyEmptyLanguageTree(string srcLangDir, string dstLangDir)
    {
        if (Directory.Exists(dstLangDir))
            Directory.Delete(dstLangDir, recursive: true);
        Directory.CreateDirectory(dstLangDir);
        foreach (var dir in Directory.EnumerateDirectories(srcLangDir, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(srcLangDir, dir);
            var target = Path.Combine(dstLangDir, rel);
            Directory.CreateDirectory(target);
        }
    }
}

public readonly record struct TranslationTask(string Origin, string RelativePath, string FromIso, string ToIso);
