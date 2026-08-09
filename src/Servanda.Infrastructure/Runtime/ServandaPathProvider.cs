namespace Servanda.Infrastructure.Runtime;

public sealed class ServandaPathProvider
{
    private readonly Func<string, string?> _getEnvironmentVariable;
    private readonly string _homeDirectory;
    private readonly uint _effectiveUserId;

    public ServandaPathProvider()
        : this(Environment.GetEnvironmentVariable, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), GetEffectiveUserId())
    {
    }

    public ServandaPathProvider(Func<string, string?> getEnvironmentVariable, string homeDirectory)
        : this(getEnvironmentVariable, homeDirectory, GetEffectiveUserId())
    {
    }

    public ServandaPathProvider(
        Func<string, string?> getEnvironmentVariable,
        string homeDirectory,
        uint effectiveUserId)
    {
        ArgumentNullException.ThrowIfNull(getEnvironmentVariable);
        ArgumentException.ThrowIfNullOrWhiteSpace(homeDirectory);

        _getEnvironmentVariable = getEnvironmentVariable;
        _homeDirectory = Path.GetFullPath(homeDirectory);
        _effectiveUserId = effectiveUserId;
    }

    public ServandaPaths CreateAndVerify()
    {
        LinuxIdentity.EnsureLinux();

        var runtimeBase = _getEnvironmentVariable("XDG_RUNTIME_DIR");
        var runtimeDirectory = string.IsNullOrWhiteSpace(runtimeBase)
            ? Path.Combine(Path.GetTempPath(), $"servanda-runtime-{_effectiveUserId}")
            : Path.Combine(RequireAbsolutePath(runtimeBase, "XDG_RUNTIME_DIR"), "servanda");

        var stateBase = _getEnvironmentVariable("XDG_STATE_HOME");
        var stateDirectory = Path.Combine(
            string.IsNullOrWhiteSpace(stateBase)
                ? Path.Combine(_homeDirectory, ".local", "state")
                : RequireAbsolutePath(stateBase, "XDG_STATE_HOME"),
            "servanda");

        PrivateFileSystem.EnsureDirectory(runtimeDirectory, _effectiveUserId);
        PrivateFileSystem.EnsureDirectory(stateDirectory, _effectiveUserId);

        return new ServandaPaths(runtimeDirectory, stateDirectory);
    }

    private static string RequireAbsolutePath(string path, string variableName)
    {
        if (!Path.IsPathFullyQualified(path))
        {
            throw new InvalidOperationException($"{variableName} musi wskazywać ścieżkę bezwzględną.");
        }

        return Path.GetFullPath(path);
    }

    private static uint GetEffectiveUserId()
    {
        LinuxIdentity.EnsureLinux();
        return LinuxIdentity.geteuid();
    }
}
