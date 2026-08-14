using Servanda.Domain.Catalog;

namespace Servanda.Domain.Tests.Catalog;

public sealed class TagTests
{
    private static readonly DateTimeOffset Timestamp = new(2026, 8, 14, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CreateNormalizesNameForUniquenessWithinArea()
    {
        var first = Tag.Create("01TAG1", "01AREA", "Łódź", Timestamp, out _);
        var second = Tag.Create("01TAG2", "01AREA", "lodz", Timestamp, out _);

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(first.NormalizedName, second.NormalizedName);
        Assert.Equal("Łódź", first.Name);
    }

    [Fact]
    public void RenameIncrementsRevisionAndNormalizedName()
    {
        var tag = Tag.Create("01TAG1", "01AREA", "Stary", Timestamp, out _)!;

        var errors = tag.Rename("Nowy Tag", Timestamp.AddMinutes(1));

        Assert.Empty(errors);
        Assert.Equal("nowy tag", tag.NormalizedName);
        Assert.Equal(2, tag.Revision);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("---")]
    public void CreateRejectsNameWithoutLetterOrDigit(string name)
    {
        var tag = Tag.Create("01TAG1", "01AREA", name, Timestamp, out var errors);

        Assert.Null(tag);
        Assert.Contains(nameof(Tag.Name), errors.Keys);
    }
}
