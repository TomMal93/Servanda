namespace Servanda.App.Security;

public sealed class ProcessSessionStore
{
    public const string CookieName = "Servanda.Session";
    public const int MaximumSessions = 64;
    public static readonly TimeSpan IdleLifetime = TimeSpan.FromDays(7);

    private readonly Lock _gate = new();
    private readonly Dictionary<string, DateTimeOffset> _sessions = new(StringComparer.Ordinal);
    private readonly TimeProvider _timeProvider;

    public ProcessSessionStore(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    public string Create()
    {
        var session = SecurityToken.Create(32);
        var fingerprint = SecurityToken.Fingerprint(session);
        var now = _timeProvider.GetUtcNow();

        lock (_gate)
        {
            RemoveExpiredSessions(now);
            if (_sessions.Count >= MaximumSessions)
            {
                var oldestSession = _sessions.MinBy(entry => entry.Value).Key;
                _sessions.Remove(oldestSession);
            }

            if (!_sessions.TryAdd(fingerprint, now.Add(IdleLifetime)))
            {
                throw new InvalidOperationException("Nie udało się utworzyć unikalnej sesji procesu.");
            }
        }

        return session;
    }

    public bool IsValid(string? session)
    {
        if (string.IsNullOrWhiteSpace(session) || session.Length > 128)
        {
            return false;
        }

        var fingerprint = SecurityToken.Fingerprint(session);
        var now = _timeProvider.GetUtcNow();
        lock (_gate)
        {
            if (!_sessions.TryGetValue(fingerprint, out var expiresAt))
            {
                return false;
            }

            if (expiresAt < now)
            {
                _sessions.Remove(fingerprint);
                return false;
            }

            _sessions[fingerprint] = now.Add(IdleLifetime);
            return true;
        }
    }

    private void RemoveExpiredSessions(DateTimeOffset now)
    {
        foreach (var session in _sessions)
        {
            if (session.Value < now)
            {
                _sessions.Remove(session.Key);
            }
        }
    }
}
