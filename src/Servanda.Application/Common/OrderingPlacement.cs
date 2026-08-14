namespace Servanda.Application.Common;

/// <summary>
/// Wylicza identyfikator elementu, przed którym należy umieścić przenoszony rekord,
/// zgodnie z kontraktem komend kolejności ADR 0004.
/// </summary>
public static class OrderingPlacement
{
    public static bool TryMoveByOffset(
        IReadOnlyList<string> orderedIds,
        string movingId,
        int offset,
        out string? beforeId)
    {
        ArgumentNullException.ThrowIfNull(orderedIds);
        ArgumentException.ThrowIfNullOrWhiteSpace(movingId);
        if (offset is not (-1 or 1))
        {
            throw new ArgumentOutOfRangeException(nameof(offset), "Dozwolony jest wyłącznie ruch o jedną pozycję.");
        }

        var index = IndexOf(orderedIds, movingId);
        if (offset < 0 && index > 0)
        {
            beforeId = orderedIds[index - 1];
            return true;
        }

        if (offset > 0 && index >= 0 && index < orderedIds.Count - 1)
        {
            beforeId = index + 2 < orderedIds.Count ? orderedIds[index + 2] : null;
            return true;
        }

        beforeId = null;
        return false;
    }

    private static int IndexOf(IReadOnlyList<string> values, string value)
    {
        for (var index = 0; index < values.Count; index++)
        {
            if (string.Equals(values[index], value, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }
}
