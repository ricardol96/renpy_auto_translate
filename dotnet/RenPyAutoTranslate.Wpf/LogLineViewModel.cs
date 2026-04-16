namespace RenPyAutoTranslate.Wpf;

public sealed class LogLineViewModel
{
    public LogLineViewModel(string timestamp, string levelLabel, string message, LogLineDisplayKind kind)
    {
        Timestamp = timestamp;
        LevelLabel = levelLabel;
        Message = message;
        Kind = kind;
    }

    public string Timestamp { get; }
    public string LevelLabel { get; }
    public string Message { get; }
    public LogLineDisplayKind Kind { get; }
}
