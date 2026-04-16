namespace RenPyAutoTranslate.Core.Renpy;

/// <summary>Raised when a translated string cannot be merged into the following line (unexpected .rpy shape).</summary>
public sealed class RenpyLineFillException : InvalidOperationException
{
    public RenpyLineFillException(string message) : base(message)
    {
    }
}
