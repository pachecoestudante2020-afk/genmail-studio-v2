using System.Diagnostics;

namespace GenMail.Wpf.Services;

public sealed class FolderOpenService
{
    public void Open(string folder)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = folder,
            UseShellExecute = true,
        });
    }
}
