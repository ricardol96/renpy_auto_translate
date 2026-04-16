using CommunityToolkit.Mvvm.ComponentModel;

namespace RenPyAutoTranslate.Wpf;

public partial class LanguageOptionViewModel : ObservableObject
{
    [ObservableProperty] private string _folderName = "";
    [ObservableProperty] private bool _isSelected;
}
