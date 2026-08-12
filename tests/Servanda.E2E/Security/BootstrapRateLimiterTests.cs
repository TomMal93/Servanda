using Servanda.App.Security;

namespace Servanda.E2E.Security;

public sealed class BootstrapRateLimiterTests
{
    [Fact]
    public async Task TicketAndSessionBootstrapHaveIndependentLimits()
    {
        await using var limiter = new BootstrapRateLimiter();

        for (var attempt = 0; attempt < BootstrapRateLimiter.PermitLimit; attempt++)
        {
            using var ticketLease = limiter.AttemptTicketIssue();
            using var bootstrapLease = limiter.AttemptSessionBootstrap();
            Assert.True(ticketLease.IsAcquired);
            Assert.True(bootstrapLease.IsAcquired);
        }

        using var rejectedTicketLease = limiter.AttemptTicketIssue();
        using var rejectedBootstrapLease = limiter.AttemptSessionBootstrap();
        Assert.False(rejectedTicketLease.IsAcquired);
        Assert.False(rejectedBootstrapLease.IsAcquired);
    }
}
