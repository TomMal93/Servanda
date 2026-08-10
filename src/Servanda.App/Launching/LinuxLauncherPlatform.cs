using System.Diagnostics;
using System.ComponentModel;
using System.Runtime.InteropServices;

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

        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = executable,
                UseShellExecute = false,
                CreateNoWindow = true,
                ArgumentList = { "--host" },
            });
            return process is not null;
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException)
        {
            return false;
        }
    }

    public bool OpenBrowser(string address)
    {
        var startInfo = CreateBrowserStartInfo(
            address,
            Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR"),
            GetEffectiveUserId(),
            Directory.Exists);

        try
        {
            using var process = Process.Start(startInfo);
            return process is not null;
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException)
        {
            return false;
        }
    }

    internal static ProcessStartInfo CreateBrowserStartInfo(
        string address,
        string? currentRuntimeDirectory,
        uint effectiveUserId,
        Func<string, bool> directoryExists)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "xdg-open",
            UseShellExecute = false,
            CreateNoWindow = true,
            ArgumentList = { address },
        };

        if (!string.IsNullOrWhiteSpace(currentRuntimeDirectory))
        {
            var systemRuntimeDirectory = $"/run/user/{effectiveUserId}";
            if (!string.Equals(
                    Path.GetFullPath(currentRuntimeDirectory),
                    systemRuntimeDirectory,
                    StringComparison.Ordinal))
            {
                if (directoryExists(systemRuntimeDirectory))
                {
                    startInfo.Environment["XDG_RUNTIME_DIR"] = systemRuntimeDirectory;
                }
                else
                {
                    startInfo.Environment.Remove("XDG_RUNTIME_DIR");
                }
            }
        }

        return startInfo;
    }

    private static uint GetEffectiveUserId()
    {
        if (!OperatingSystem.IsLinux())
        {
            throw new PlatformNotSupportedException("Launcher Servandy wymaga systemu Linux.");
        }

        return geteuid();
    }

    [DllImport("libc", SetLastError = true)]
    private static extern uint geteuid();
}
