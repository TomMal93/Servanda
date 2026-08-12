namespace Servanda.App.Security;

public sealed class BootstrapTicketStore
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(60);

    private readonly Lock _gate = new();
    private readonly Dictionary<string, DateTimeOffset> _tickets = new(StringComparer.Ordinal);
    private readonly TimeProvider _timeProvider;

    public BootstrapTicketStore(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    public string Issue()
    {
        var ticket = SecurityToken.Create(24);
        var fingerprint = SecurityToken.Fingerprint(ticket);
        var now = _timeProvider.GetUtcNow();

        lock (_gate)
        {
            RemoveExpiredTickets(now);
            if (!_tickets.TryAdd(fingerprint, now.Add(Lifetime)))
            {
                throw new InvalidOperationException("Nie udało się utworzyć unikalnego biletu startowego.");
            }
        }

        return ticket;
    }

    public bool TryConsume(string? ticket) => TryConsume(ticket, static () => true);

    public bool TryConsume(string? ticket, Func<bool> tryCompleteExchange)
    {
        ArgumentNullException.ThrowIfNull(tryCompleteExchange);

        if (string.IsNullOrWhiteSpace(ticket) || ticket.Length > 128)
        {
            return false;
        }

        var fingerprint = SecurityToken.Fingerprint(ticket);
        var now = _timeProvider.GetUtcNow();
        lock (_gate)
        {
            if (!_tickets.TryGetValue(fingerprint, out var expiresAt))
            {
                return false;
            }

            if (expiresAt < now)
            {
                _tickets.Remove(fingerprint);
                return false;
            }

            if (!tryCompleteExchange())
            {
                return false;
            }

            _tickets.Remove(fingerprint);
            return true;
        }
    }

    private void RemoveExpiredTickets(DateTimeOffset now)
    {
        foreach (var ticket in _tickets)
        {
            if (ticket.Value < now)
            {
                _tickets.Remove(ticket.Key);
            }
        }
    }
}
