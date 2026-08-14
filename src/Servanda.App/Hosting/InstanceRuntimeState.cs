using Servanda.Infrastructure.Runtime;

namespace Servanda.App.Hosting;

public sealed class InstanceRuntimeState
{
    private readonly Lock _gate = new();
    private readonly string _instanceId = Guid.NewGuid().ToString("N");
    private Uri? _origin;
    private OperationalState _operationalState = OperationalState.Starting;

    public string InstanceId => _instanceId;

    public Uri Origin
    {
        get
        {
            lock (_gate)
            {
                return _origin
                    ?? throw new InvalidOperationException("Host nie opublikował jeszcze kanonicznego originu.");
            }
        }
    }

    public bool HasOrigin
    {
        get
        {
            lock (_gate)
            {
                return _origin is not null;
            }
        }
    }

    public bool IsReady
    {
        get
        {
            lock (_gate)
            {
                return _origin is not null && _operationalState == OperationalState.Ready;
            }
        }
    }

    public bool IsRecovery
    {
        get
        {
            lock (_gate)
            {
                return _origin is not null && _operationalState == OperationalState.Recovery;
            }
        }
    }

    public string DescriptorState
    {
        get
        {
            lock (_gate)
            {
                return _origin is null ? "starting" : ToDescriptorState(_operationalState);
            }
        }
    }

    public void MarkDatabaseReady()
    {
        lock (_gate)
        {
            _operationalState = OperationalState.Ready;
        }
    }

    public void MarkRecovery()
    {
        lock (_gate)
        {
            _operationalState = OperationalState.Recovery;
        }
    }

    public void AttachOrigin(Uri origin)
    {
        ArgumentNullException.ThrowIfNull(origin);
        if (!origin.IsLoopback || !string.Equals(origin.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Kanoniczny origin Servandy musi używać HTTP na loopbacku.");
        }

        lock (_gate)
        {
            if (_origin is not null)
            {
                throw new InvalidOperationException("Kanoniczny origin został już ustawiony.");
            }

            if (_operationalState == OperationalState.Starting)
            {
                throw new InvalidOperationException("Stan bazy musi zostać ustalony przed publikacją originu.");
            }

            _origin = origin;
        }
    }

    public InstanceDescriptor CreateDescriptor()
    {
        lock (_gate)
        {
            var origin = _origin
                ?? throw new InvalidOperationException("Host nie opublikował jeszcze kanonicznego originu.");
            var descriptor = InstanceDescriptor.Starting(
                _instanceId,
                Environment.ProcessId,
                origin.GetLeftPart(UriPartial.Authority));
            return _operationalState switch
            {
                OperationalState.Ready => descriptor.Ready(),
                OperationalState.Recovery => descriptor.Recovery(),
                _ => throw new InvalidOperationException("Stan bazy nie pozwala opublikować deskryptora."),
            };
        }
    }

    private static string ToDescriptorState(OperationalState state) => state switch
    {
        OperationalState.Ready => "ready",
        OperationalState.Recovery => "recovery",
        _ => "starting",
    };

    private enum OperationalState
    {
        Starting,
        Ready,
        Recovery,
    }
}
