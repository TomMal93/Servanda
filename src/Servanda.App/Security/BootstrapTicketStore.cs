using System.Collections.Concurrent;

namespace Servanda.App.Security;

public sealed class BootstrapTicketStore
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(60);

    private readonly ConcurrentDictionary<string, DateTimeOffset> _tickets = new(StringComparer.Ordinal);
    private readonly TimeProvider _timeProvider;

    public BootstrapTicketStore(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    public string Issue()
    {
        RemoveExpiredTickets();
        var ticket = SecurityToken.Create(24);
        var expiresAt = _timeProvider.GetUtcNow().Add(Lifetime);
        if (!_tickets.TryAdd(SecurityToken.Fingerprint(ticket), expiresAt))
        {
            throw new InvalidOperationException("Nie udało się utworzyć unikalnego biletu startowego.");
        }

        return ticket;
    }

    public bool TryConsume(string? ticket)
    {
        if (string.IsNullOrWhiteSpace(ticket) || ticket.Length > 128)
        {
            return false;
        }

        return _tickets.TryRemove(SecurityToken.Fingerprint(ticket), out var expiresAt)
            && expiresAt >= _timeProvider.GetUtcNow();
    }

    private void RemoveExpiredTickets()
    {
        var now = _timeProvider.GetUtcNow();
        foreach (var ticket in _tickets)
        {
            if (ticket.Value < now)
            {
                _tickets.TryRemove(ticket.Key, out _);
            }
        }
    }
}
