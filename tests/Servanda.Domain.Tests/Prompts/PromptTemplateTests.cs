using Servanda.Domain.Prompts;

namespace Servanda.Domain.Tests.Prompts;

public sealed class PromptTemplateTests
{
    [Fact]
    public void ExtractPlaceholdersReturnsUniqueNamesInOrderOfAppearance()
    {
        var names = PromptTemplate.ExtractPlaceholders("{{temat}} i {{ styl }} oraz {{temat}}");

        Assert.Equal(["temat", "styl"], names);
    }

    [Fact]
    public void ExtractPlaceholdersIgnoresInvalidMarker()
    {
        Assert.Empty(PromptTemplate.ExtractPlaceholders("{{1temat}} {{ }} {{temat"));
    }

    [Fact]
    public void RenderReplacesKnownValuesAndEmptiesRemainingMarkers()
    {
        var result = PromptTemplate.Render(
            "Napisz o {{temat}} w stylu {{styl}}.",
            new Dictionary<string, string> { ["temat"] = "Servandzie" });

        Assert.Equal("Napisz o Servandzie w stylu .", result);
    }

    [Fact]
    public void RenderDoesNotInterpretValueAsAnotherMarker()
    {
        var result = PromptTemplate.Render(
            "{{temat}}",
            new Dictionary<string, string> { ["temat"] = "{{styl}}", ["styl"] = "nieużyty" });

        Assert.Equal("{{styl}}", result);
    }
}
