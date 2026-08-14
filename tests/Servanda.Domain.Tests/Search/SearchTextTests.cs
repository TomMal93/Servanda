using Servanda.Domain.Search;

namespace Servanda.Domain.Tests.Search;

public sealed class SearchTextTests
{
    [Theory]
    [InlineData("Łódź", "lodz")]
    [InlineData("łódź", "lodz")]
    [InlineData("lodz", "lodz")]
    [InlineData("ŁÓDŹ", "lodz")]
    [InlineData("ĄĆĘŁŃÓŚŹŻ", "acelnoszz")]
    [InlineData("ąćęłńóśźż", "acelnoszz")]
    public void NormalizeMakesPolishSpellingVariantsEqual(string value, string expected)
    {
        Assert.Equal(expected, SearchText.Normalize(value));
    }

    [Fact]
    public void NormalizeTreatsDecomposedAndComposedFormsAsEqual()
    {
        const string composed = "ó";
        const string decomposed = "ó";

        Assert.Equal(SearchText.Normalize(composed), SearchText.Normalize(decomposed));
    }

    [Theory]
    [InlineData("docs.example.com", "docs example com")]
    [InlineData("  wiele   odstępów  ", "wiele odstepow")]
    [InlineData("prompt: \"studio\" -test", "prompt studio test")]
    public void NormalizeSplitsPunctuationIntoTokens(string value, string expected)
    {
        Assert.Equal(expected, SearchText.Normalize(value));
    }

    [Fact]
    public void BuildPrefixQueryCreatesConjunctionOfQuotedPrefixes()
    {
        Assert.Equal("\"kon\"* \"prom\"*", SearchText.BuildPrefixQuery("koń prom"));
    }

    [Theory]
    [InlineData("AND OR NOT", "\"and\"* \"or\"* \"not\"*")]
    [InlineData("\"cytat\" * -minus", "\"cytat\"* \"minus\"*")]
    public void BuildPrefixQueryNeutralizesTextResemblingFtsSyntax(string value, string expected)
    {
        Assert.Equal(expected, SearchText.BuildPrefixQuery(value));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("a")]
    [InlineData("*")]
    public void BuildPrefixQueryRejectsQueryShorterThanTwoNormalizedCharacters(string value)
    {
        Assert.Null(SearchText.BuildPrefixQuery(value));
    }

    [Fact]
    public void IsQueryTooShortDistinguishesSingleCharacterFromEmptyQuery()
    {
        Assert.True(SearchText.IsQueryTooShort("a"));
        Assert.False(SearchText.IsQueryTooShort(""));
        Assert.False(SearchText.IsQueryTooShort("ab"));
    }
}
