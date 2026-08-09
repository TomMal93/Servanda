namespace Servanda.App.Hosting;

public sealed class InstanceRuntimeState
{
    private readonly string _instanceId = Guid.NewGuid().ToString("N");
    private Uri? _origin;

    public string InstanceId => _instanceId;

    public Uri Origin => Volatile.Read(ref _origin)
        ?? throw new InvalidOperationException("Host nie opublikował jeszcze kanonicznego originu.");

    public bool IsReady => Volatile.Read(ref _origin) is not null;

    public void MarkReady(Uri origin)
    {
        ArgumentNullException.ThrowIfNull(origin);

        if (!origin.IsLoopback || !string.Equals(origin.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Kanoniczny origin Servandy musi używać HTTP na loopbacku.");
        }

        if (Interlocked.CompareExchange(ref _origin, origin, null) is not null)
        {
            throw new InvalidOperationException("Kanoniczny origin został już ustawiony.");
        }
    }
}
