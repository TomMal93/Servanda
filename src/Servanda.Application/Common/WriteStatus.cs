namespace Servanda.Application.Common;

/// <summary>
/// Wspólny wynik komendy zapisu: sukces, walidacja, konflikt albo brak rekordu.
/// </summary>
public enum WriteStatus
{
    Success,
    ValidationFailed,
    Conflict,
    NotFound,
}
