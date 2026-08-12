namespace Servanda.Infrastructure.Runtime;

public sealed record ServandaPaths(string RuntimeDirectory, string StateDirectory, string DataDirectory)
{
    public ServandaPaths(string runtimeDirectory, string stateDirectory)
        : this(runtimeDirectory, stateDirectory, Path.Combine(stateDirectory, "data"))
    {
    }

    public string DescriptorPath => Path.Combine(RuntimeDirectory, "instance.json");

    public string ControlSecretPath => Path.Combine(RuntimeDirectory, "control.secret");

    public string InstanceLockPath => Path.Combine(RuntimeDirectory, "instance.lock");

    public string TechnicalLogPath => Path.Combine(StateDirectory, "servanda.log");

    public string DatabasePath => Path.Combine(DataDirectory, "servanda.db");

    public string DatabaseLockPath => Path.Combine(DataDirectory, "servanda.lock");

    public string BackupsDirectory => Path.Combine(DataDirectory, "backups");

    public string ExportsDirectory => Path.Combine(DataDirectory, "exports");
}
