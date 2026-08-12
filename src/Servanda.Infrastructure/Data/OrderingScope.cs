namespace Servanda.Infrastructure.Data;

internal sealed class OrderingScope
{
    public string ScopeKey { get; set; } = string.Empty;

    public long Revision { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
