using Servanda.Domain.Tools;

namespace Servanda.Domain.Tests.Tools;

public sealed class ToolTests
{
    private static readonly DateTimeOffset Timestamp = new(2026, 8, 14, 10, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("https://example.com")]
    [InlineData("http://example.com/sciezka?x=1")]
    public void CreateAcceptsHttpAndHttpsAddresses(string url)
    {
        var tool = Create(url);

        Assert.NotNull(tool);
        Assert.Equal(url, tool.Url);
        Assert.Equal(1, tool.Revision);
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("file:///etc/passwd")]
    [InlineData("example.com")]
    [InlineData("")]
    public void CreateRejectsAddressOutsideHttpAndHttps(string url)
    {
        var tool = Tool.Create(
            "01TOOL",
            "01AREA",
            "01CATEGORY",
            "Narzędzie",
            "Opis narzędzia",
            url,
            Tool.RegularGroup,
            [],
            0,
            Timestamp,
            out var errors);

        Assert.Null(tool);
        Assert.Contains(nameof(Tool.Url), errors.Keys);
    }

    [Fact]
    public void UpdateContentReplacesTagsAndIncrementsRevision()
    {
        var tool = Create("https://example.com");

        var errors = tool!.UpdateContent(
            "Nowa nazwa",
            "Nowy opis",
            "https://example.com/nowy",
            ["01TAG1", "01TAG2"],
            Timestamp.AddMinutes(1));

        Assert.Empty(errors);
        Assert.Equal(2, tool.Revision);
        Assert.Equal(["01TAG1", "01TAG2"], tool.Tags.Select(tag => tag.TagId));
    }

    [Fact]
    public void UpdateContentRejectsMoreThanEightTags()
    {
        var tool = Create("https://example.com");
        var tags = Enumerable.Range(0, Tool.MaxTags + 1).Select(index => $"01TAG{index}").ToArray();

        var errors = tool!.UpdateContent("Nazwa", "Opis", "https://example.com", tags, Timestamp);

        Assert.Contains(nameof(Tool.Tags), errors.Keys);
        Assert.Equal(1, tool.Revision);
    }

    [Fact]
    public void MoveToChangesMembershipAndIncrementsRevision()
    {
        var tool = Create("https://example.com");

        tool!.MoveTo("01OTHER", Tool.FeaturedGroup, Timestamp.AddMinutes(1));

        Assert.Equal("01OTHER", tool.CategoryId);
        Assert.Equal(Tool.FeaturedGroup, tool.GroupKey);
        Assert.Equal(2, tool.Revision);
    }

    private static Tool? Create(string url) => Tool.Create(
        "01TOOL",
        "01AREA",
        "01CATEGORY",
        "Narzędzie",
        "Opis narzędzia",
        url,
        Tool.RegularGroup,
        [],
        0,
        Timestamp,
        out _);
}
