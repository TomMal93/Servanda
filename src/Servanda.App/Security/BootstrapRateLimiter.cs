using System.Threading.RateLimiting;

namespace Servanda.App.Security;

public sealed class BootstrapRateLimiter : IAsyncDisposable
{
    public const int PermitLimit = 10;
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    private readonly FixedWindowRateLimiter _ticketIssues = CreateLimiter();
    private readonly FixedWindowRateLimiter _sessionBootstraps = CreateLimiter();

    public RateLimitLease AttemptTicketIssue() => _ticketIssues.AttemptAcquire();

    public RateLimitLease AttemptSessionBootstrap() => _sessionBootstraps.AttemptAcquire();

    public async ValueTask DisposeAsync()
    {
        await _ticketIssues.DisposeAsync();
        await _sessionBootstraps.DisposeAsync();
    }

    private static FixedWindowRateLimiter CreateLimiter() => new(new FixedWindowRateLimiterOptions
    {
        PermitLimit = PermitLimit,
        Window = Window,
        QueueLimit = 0,
        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
        AutoReplenishment = true,
    });
}
