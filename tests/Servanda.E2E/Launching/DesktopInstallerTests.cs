using System.Diagnostics;
using System.Runtime.Versioning;

namespace Servanda.E2E.Launching;

[SupportedOSPlatform("linux")]
public sealed class DesktopInstallerTests
{
    [Fact]
    public async Task InstallerAndUninstallerOnlyManageUserDesktopEntry()
    {
        var temporaryPath = Path.Combine(Path.GetTempPath(), $"servanda-desktop-tests-{Guid.NewGuid():N}");
        var packagePath = Path.Combine(temporaryPath, "portable package");
        Directory.CreateDirectory(packagePath);

        try
        {
            var packagingSource = Path.Combine(AppContext.BaseDirectory, "packaging");
            foreach (var fileName in new[] { "install-desktop.sh", "uninstall-desktop.sh", "servanda.desktop.in" })
            {
                File.Copy(Path.Combine(packagingSource, fileName), Path.Combine(packagePath, fileName));
            }

            var executablePath = Path.Combine(packagePath, "Servanda");
            await File.WriteAllTextAsync(executablePath, "test executable");
            File.SetUnixFileMode(executablePath, UnixFileMode.UserRead | UnixFileMode.UserExecute);

            var dataHome = Path.Combine(temporaryPath, "data");
            var installResult = await RunScriptAsync(Path.Combine(packagePath, "install-desktop.sh"), dataHome);
            Assert.Equal(0, installResult);

            var desktopPath = Path.Combine(dataHome, "applications", "servanda.desktop");
            var desktopEntry = await File.ReadAllTextAsync(desktopPath);
            Assert.Contains($"Exec=\"{executablePath}\"", desktopEntry, StringComparison.Ordinal);

            var uninstallResult = await RunScriptAsync(Path.Combine(packagePath, "uninstall-desktop.sh"), dataHome);
            Assert.Equal(0, uninstallResult);
            Assert.False(File.Exists(desktopPath));
            Assert.True(File.Exists(executablePath));
        }
        finally
        {
            Directory.Delete(temporaryPath, recursive: true);
        }
    }

    private static async Task<int> RunScriptAsync(string scriptPath, string dataHome)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "/bin/sh",
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            ArgumentList = { scriptPath },
        };
        startInfo.Environment["XDG_DATA_HOME"] = dataHome;

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Nie udało się uruchomić skryptu testowego.");
        await process.WaitForExitAsync();
        return process.ExitCode;
    }
}
