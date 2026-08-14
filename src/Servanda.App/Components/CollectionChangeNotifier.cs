namespace Servanda.App.Components;

/// <summary>
/// Informuje wszystkie aktywne circuity, że operacja zastąpiła całą kolekcję.
/// Edytory z niezapisanymi zmianami zachowują roboczą kopię, aby zapis zakończył się konfliktem epoki.
/// </summary>
public sealed class CollectionChangeNotifier
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
            try
            {
                await handler();
            }
            catch (Exception exception) when (exception is InvalidOperationException or TaskCanceledException)
            {
                // Powiadomienie jest best-effort: rozłączony circuit nie może zmienić udanego importu w błąd.
            }
        }
    }
}
