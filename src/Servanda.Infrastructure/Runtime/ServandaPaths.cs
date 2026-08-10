namespace Servanda.Infrastructure.Runtime;

public sealed record ServandaPaths(string RuntimeDirectory, string StateDirectory)
{
    public string DescriptorPath => Path.Combine(RuntimeDirectory, "instance.json");

    public string ControlSecretPath => Path.Combine(RuntimeDirectory, "control.secret");

    public string InstanceLockPath => Path.Combine(RuntimeDirectory, "instance.lock");

    public string TechnicalLogPath => Path.Combine(StateDirectory, "servanda.log");
}
