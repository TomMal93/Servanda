using Servanda.Application.Common;

namespace Servanda.Application.Tests;

public sealed class OrderingPlacementTests
{
    [Theory]
    [InlineData("b", -1, true, "a")]
    [InlineData("b", 1, true, null)]
    [InlineData("a", -1, false, null)]
    [InlineData("c", 1, false, null)]
    [InlineData("missing", 1, false, null)]
    public void ComputesCanonicalBeforeIdentifier(
        string movingId,
        int offset,
        bool expectedMove,
        string? expectedBeforeId)
    {
        var moved = OrderingPlacement.TryMoveByOffset(["a", "b", "c"], movingId, offset, out var beforeId);

        Assert.Equal(expectedMove, moved);
        Assert.Equal(expectedBeforeId, beforeId);
    }

    [Fact]
    public void RejectsOffsetsLargerThanOnePosition()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            OrderingPlacement.TryMoveByOffset(["a", "b"], "a", 2, out _));
    }
}
