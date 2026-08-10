using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using System.Runtime.Versioning;
using Servanda.Infrastructure.Diagnostics;
using Servanda.Infrastructure.Runtime;

namespace Servanda.App.Hosting;

[SupportedOSPlatform("linux")]
public sealed class InstanceLifecyclePublisher : IHostedLifecycleService
{
    private readonly IServer _server;
    private readonly InstanceRuntimeState _runtimeState;
    private readonly AtomicInstanceDescriptorStore _descriptorStore;
    private readonly TechnicalLogWriter _technicalLog;

    public InstanceLifecyclePublisher(
        IServer server,
        InstanceRuntimeState runtimeState,
        AtomicInstanceDescriptorStore descriptorStore,
        TechnicalLogWriter technicalLog)
    {
        _server = server;
        _runtimeState = runtimeState;
        _descriptorStore = descriptorStore;
        _technicalLog = technicalLog;
    }

    public Task StartingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public async Task StartedAsync(CancellationToken cancellationToken)
    {
        var addresses = _server.Features.Get<IServerAddressesFeature>()?.Addresses
            ?? throw new InvalidOperationException("Kestrel nie udostępnił związanego adresu.");
        var address = addresses.Single();
        var origin = new Uri(address, UriKind.Absolute);
        if (!origin.IsLoopback)
        {
            throw new InvalidOperationException("Kestrel związał adres inny niż loopback.");
        }

        var starting = InstanceDescriptor.Starting(
            _runtimeState.InstanceId,
            Environment.ProcessId,
            origin.GetLeftPart(UriPartial.Authority));

        await _descriptorStore.PublishAsync(starting, cancellationToken);
        await _technicalLog.WriteAsync(TechnicalEvent.HostReady, cancellationToken);
        _runtimeState.MarkReady(origin);
        await _descriptorStore.PublishAsync(starting.Ready(), cancellationToken);
    }

    public Task StoppingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StoppedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
