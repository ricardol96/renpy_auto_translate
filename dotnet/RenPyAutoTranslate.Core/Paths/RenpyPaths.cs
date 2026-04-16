using System.IO;

namespace RenPyAutoTranslate.Core.Paths;

public static class RenpyPaths
{
    /// <summary>Folder containing exe when deployed; dev: parent of dotnet output or repo.</summary>
    public static string ToolRepoRootFromBaseDirectory(string? baseDirectory = null)
    {
        baseDirectory ??= AppContext.BaseDirectory;
        var dir = Path.GetFullPath(baseDirectory.TrimEnd(Path.DirectorySeparatorChar));
        var name = Path.GetFileName(dir);
        if (string.Equals(name, "src", StringComparison.OrdinalIgnoreCase))
            return Path.GetFullPath(Path.Combine(dir, ".."));
        return dir;
    }

    public static string LogsDirectory(string toolRoot) =>
        Path.Combine(toolRoot, "logs");

    /// <exception cref="ArgumentException">Invalid game folder name.</exception>
    public static string OutputTlPath(string repoRoot, string gameName)
    {
        var gn = gameName.Trim();
        if (string.IsNullOrEmpty(gn))
            throw new ArgumentException("Game name is empty.", nameof(gameName));
        if (gn is "." or ".." || gn.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            throw new ArgumentException($"Invalid game folder name: {gameName}", nameof(gameName));
        return Path.GetFullPath(Path.Combine(repoRoot, gn, "game", "tl"));
    }

    /// <exception cref="ArgumentException">Path is not …/game/tl.</exception>
    public static string GameNameFromTlPath(string tlRoot)
    {
        tlRoot = Path.GetFullPath(tlRoot);
        if (string.IsNullOrEmpty(tlRoot) || !Directory.Exists(tlRoot))
            throw new ArgumentException("Invalid TL path.");
        if (!string.Equals(Path.GetFileName(tlRoot), "tl", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException(
                "Select the folder named \"tl\" inside the game directory (path must be …/<game name>/game/tl).");
        var gameDir = Path.GetDirectoryName(tlRoot);
        if (string.IsNullOrEmpty(gameDir))
            throw new ArgumentException("Invalid TL path.");
        if (!string.Equals(Path.GetFileName(gameDir), "game", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException(
                "The folder above \"tl\" must be named \"game\" (expected …/<game name>/game/tl).");
        var projectDir = Path.GetDirectoryName(gameDir);
        if (string.IsNullOrEmpty(projectDir))
            throw new ArgumentException("Could not determine the game folder name from this path.");
        var name = Path.GetFileName(projectDir);
        if (string.IsNullOrEmpty(name) || name is "." or "..")
            throw new ArgumentException("Could not determine the game folder name from this path.");
        return name;
    }

    public static bool PathsEqualCaseInsensitive(string a, string b) =>
        string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);
}
