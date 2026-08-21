namespace Servanda.Infrastructure.Persistence;

public static class DbLocationHelper
{
    public static string ResolveConnectionString(string? rawConnectionString, string baseDirectory)
    {
        if (string.IsNullOrWhiteSpace(rawConnectionString))
        {
            rawConnectionString = "Data Source=data/servanda.db";
        }

        const string prefix = "Data Source=";
        if (!rawConnectionString.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return rawConnectionString;
        }

        var pathPart = rawConnectionString.Substring(prefix.Length).Trim();
        if (pathPart.Equals(":memory:", StringComparison.OrdinalIgnoreCase))
        {
            return rawConnectionString;
        }

        if (Path.IsPathRooted(pathPart))
        {
            var dir = Path.GetDirectoryName(pathPart);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            return rawConnectionString;
        }

        // Search upwards for repo root containing 'data' or 'Servanda.sln'
        var currentDir = new DirectoryInfo(baseDirectory);
        DirectoryInfo? repoRoot = null;

        while (currentDir != null)
        {
            if (File.Exists(Path.Combine(currentDir.FullName, "Servanda.sln")) ||
                Directory.Exists(Path.Combine(currentDir.FullName, "data")))
            {
                repoRoot = currentDir;
                break;
            }
            currentDir = currentDir.Parent;
        }

        var targetRoot = repoRoot?.FullName ?? baseDirectory;
        var fullDbPath = Path.GetFullPath(Path.Combine(targetRoot, pathPart));
        var targetDir = Path.GetDirectoryName(fullDbPath);

        if (!string.IsNullOrEmpty(targetDir) && !Directory.Exists(targetDir))
        {
            Directory.CreateDirectory(targetDir);
        }

        return $"{prefix}{fullDbPath}";
    }
}
