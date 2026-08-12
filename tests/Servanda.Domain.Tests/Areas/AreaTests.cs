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

    [Fact]
    public void SetVisibilityChangesStateAndIncrementsRevision()
    {
        var created = new DateTimeOffset(2026, 8, 12, 10, 0, 0, TimeSpan.Zero);
        var changed = created.AddMinutes(1);
        var area = Area.CreateSeed("area-1", "Dom", "Opis", "home", "accent-0", "home", 0, created);

        area.SetVisibility(true, changed);

        Assert.True(area.IsHidden);
        Assert.Equal(2, area.Revision);
        Assert.Equal(changed, area.UpdatedAt);
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

    [Fact]
    public void CreatePlannedBuildsNormalizedCustomArea()
    {
        var timestamp = DateTimeOffset.UtcNow;

        var area = Area.CreatePlanned(
            "01J00000000000000000000008",
            "  Projekty  ",
            "  Rzeczy do zrobienia  ",
            "generic",
            "accent-2",
            7,
            timestamp,
            out var errors);

        Assert.Empty(errors);
        Assert.NotNull(area);
        Assert.Equal("Projekty", area.Name);
        Assert.Equal("Rzeczy do zrobienia", area.Description);
        Assert.Equal("custom", area.ModuleKey);
        Assert.Equal("planned", area.Availability);
        Assert.Equal(7, area.SortOrder);
        Assert.Equal(1, area.Revision);
    }

    [Theory]
    [InlineData("unknown", "accent-0", nameof(Area.IconKey))]
    [InlineData("generic", "unknown", nameof(Area.AccentKey))]
    public void CreatePlannedRejectsUnsupportedPresentation(
        string iconKey,
        string accentKey,
        string expectedError)
    {
        var area = Area.CreatePlanned(
            "01J00000000000000000000008",
            "Projekty",
            string.Empty,
            iconKey,
            accentKey,
            7,
            DateTimeOffset.UtcNow,
            out var errors);

        Assert.Null(area);
        Assert.Contains(expectedError, errors.Keys);
    }
}
