using CommunityToolkit.Mvvm.ComponentModel;
using RenPyAutoTranslate.Core;

namespace RenPyAutoTranslate.Wpf;

public partial class LanguageOptionViewModel : ObservableObject
{
    [ObservableProperty] private string _folderName = "";
    [ObservableProperty] private string _isoLabel = "—";
    [ObservableProperty] private bool _isSelected;

    partial void OnFolderNameChanged(string value)
    {
        IsoLabel = LanguageNames.TryGetIso(value, out var iso) ? iso : "—";
    }
}
