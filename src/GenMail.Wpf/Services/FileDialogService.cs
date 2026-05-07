using Microsoft.Win32;

namespace GenMail.Wpf.Services;

public sealed class FileDialogService
{
    public string? PickTxtFile()
    {
        OpenFileDialog dialog = new OpenFileDialog
        {
            Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
            CheckFileExists = true,
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}
