using mVasu.Api.Common;

namespace mVasu.Api.Tests;

/// <summary>
/// Pinpoints the contract of <see cref="FtsExpressionBuilder.Build"/>:
/// what gets sent to SQL Server's <c>CONTAINS</c> for a given user
/// input. Shared by every FTS-using service (customers, tiskilista, …).
/// The actual SQL round-trip is exercised in dev / staging where the
/// fts_Asiakas catalog exists.
/// </summary>
public class FtsExpressionBuilderTests
{
    [Theory]
    [InlineData("",          null)]
    [InlineData("   ",       null)]
    [InlineData("a",         null)]      // single char below 2-char floor
    [InlineData("AND",       null)]      // CONTAINS reserved word, dropped
    [InlineData("Or",        null)]
    [InlineData("not",       null)]
    [InlineData("near",      null)]
    public void EdgeCases_ReturnNull(string? input, string? expected)
    {
        Assert.Equal(expected, FtsExpressionBuilder.Build(input ?? string.Empty));
    }

    [Fact]
    public void SingleWord_WrapsWithPrefixWildcard()
    {
        Assert.Equal("\"koivu*\"",
            FtsExpressionBuilder.Build("koivu"));
    }

    [Fact]
    public void MultipleWords_AreAndJoined_ForNarrowingMatch()
    {
        Assert.Equal("\"matti*\" AND \"koivu*\"",
            FtsExpressionBuilder.Build("matti koivu"));
    }

    [Fact]
    public void DoubleQuotes_AreStripped()
    {
        // Otherwise CONTAINS would see "matti""smith" which it parses as
        // an unterminated phrase or worse.
        Assert.Equal("\"mattismith*\"",
            FtsExpressionBuilder.Build("matti\"smith"));
    }

    [Fact]
    public void SquareBrackets_AreStripped()
    {
        Assert.Equal("\"koivu*\"",
            FtsExpressionBuilder.Build("[koivu]"));
    }

    [Fact]
    public void ReservedWordIsDroppedButOthersKept()
    {
        // "or" alone would short-circuit to null; mixed with real terms
        // we drop the operator and keep the rest.
        Assert.Equal("\"matti*\" AND \"koivu*\"",
            FtsExpressionBuilder.Build("matti or koivu"));
    }

    [Fact]
    public void AccentedFinnishCharacters_PassThroughUnchanged()
    {
        // The fts_Asiakas catalog uses Finnish word breaking (LCID 1053)
        // so ä/ö/å are first-class — must NOT be sanitised away. Case
        // is preserved as the user typed it; CONTAINS is case-insensitive
        // by default so the original casing is irrelevant for matching.
        Assert.Equal("\"Hämäläinen*\"",
            FtsExpressionBuilder.Build("Hämäläinen"));
    }

    [Fact]
    public void TabsAndNewlines_AreTreatedAsSeparators()
    {
        Assert.Equal("\"matti*\" AND \"koivu*\"",
            FtsExpressionBuilder.Build("matti\tkoivu"));
    }
}
