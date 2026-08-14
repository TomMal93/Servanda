namespace Servanda.Infrastructure.Runtime;

public sealed record InstanceDescriptor(
    int FormatVersion,
    string InstanceId,
    int ProcessId,
    string Origin,
    string State)
{
    public const int CurrentFormatVersion = 1;

    public static InstanceDescriptor Starting(string instanceId, int processId, string origin) =>
        new(CurrentFormatVersion, instanceId, processId, origin, "starting");

    public InstanceDescriptor Ready() => this with { State = "ready" };

    public InstanceDescriptor Recovery() => this with { State = "recovery" };
}
