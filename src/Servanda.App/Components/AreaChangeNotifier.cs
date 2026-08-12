namespace Servanda.App.Components;

public sealed class AreaChangeNotifier
{
    public event Func<Task>? Changed;

    public async Task NotifyAsync()
    {
        if (Changed is null)
        {
            return;
        }

        foreach (var handler in Changed.GetInvocationList().Cast<Func<Task>>())
        {
            await handler();
        }
    }
}
