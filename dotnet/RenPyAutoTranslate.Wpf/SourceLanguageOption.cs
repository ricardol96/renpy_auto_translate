namespace RenPyAutoTranslate.Wpf;

public sealed record SourceLanguageOption(string Iso, string Name)
{
    public string DisplayName => $"{Name} ({Iso})";

    public override string ToString() => DisplayName;
}
