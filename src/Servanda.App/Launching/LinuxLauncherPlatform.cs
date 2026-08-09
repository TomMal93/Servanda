using System.Diagnostics;

namespace Servanda.App.Launching;

public sealed class LinuxLauncherPlatform : ILauncherPlatform
{
    public bool StartHost()
    {
        var executable = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executable))
        {
            return false;
        }

        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = executable,
            UseShellExecute = false,
            CreateNoWindow = true,
            ArgumentList = { "--host" },
        });
        return process is not null;
    }

    public bool OpenBrowser(string address)
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = address,
            UseShellExecute = true,
        });
        return process is not null;
    }
}
