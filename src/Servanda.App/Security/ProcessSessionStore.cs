using System.Collections.Concurrent;

namespace Servanda.App.Security;

public sealed class ProcessSessionStore
{
    public const string CookieName = "Servanda.Session";

    private readonly ConcurrentDictionary<string, byte> _sessions = new(StringComparer.Ordinal);

    public string Create()
    {
        var session = SecurityToken.Create(32);
        if (!_sessions.TryAdd(SecurityToken.Fingerprint(session), 0))
        {
            throw new InvalidOperationException("Nie udało się utworzyć unikalnej sesji procesu.");
        }

        return session;
    }

    public bool IsValid(string? session)
    {
        if (string.IsNullOrWhiteSpace(session) || session.Length > 128)
        {
            return false;
        }

        return _sessions.ContainsKey(SecurityToken.Fingerprint(session));
    }
}
