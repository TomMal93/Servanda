namespace Servanda.App.Launching;

public interface ILauncherPlatform
{
    bool StartHost();

    bool OpenBrowser(string address);
}
