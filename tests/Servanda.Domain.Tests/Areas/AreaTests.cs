using Servanda.Domain.Areas;

namespace Servanda.Domain.Tests.Areas;

public sealed class AreaTests
{
    [Fact]
    public void UpdateContentNormalizesValuesAndIncrementsRevision()
    {
        var created = new DateTimeOffset(2026, 8, 12, 10, 0, 0, TimeSpan.Zero);
        var area = Area.CreateSeed("area-1", "Dom", "Opis", "home", "accent-0", "home", 0, created);

        var errors = area.UpdateContent("  Mój dom  ", "  Nowy opis  ", created.AddMinutes(1));

        Assert.Empty(errors);
        Assert.Equal("Mój dom", area.Name);
        Assert.Equal("Nowy opis", area.Description);
        Assert.Equal(2, area.Revision);
        Assert.Equal(created.AddMinutes(1), area.UpdatedAt);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void UpdateContentRejectsEmptyName(string name)
    {
        var timestamp = DateTimeOffset.UtcNow;
        var area = Area.CreateSeed("area-1", "Dom", "Opis", "home", "accent-0", "home", 0, timestamp);

        var errors = area.UpdateContent(name, "Nowy opis", timestamp.AddMinutes(1));

        Assert.Contains(nameof(Area.Name), errors.Keys);
        Assert.Equal("Dom", area.Name);
        Assert.Equal(1, area.Revision);
    }
}
