namespace Servanda.Infrastructure.Data;

internal sealed class AppState
{
    public int Id { get; set; }

    public string ContentEpoch { get; set; } = string.Empty;
}
