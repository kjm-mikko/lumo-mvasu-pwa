using mVasu.Api.Common;

namespace mVasu.Api.Tests;

/// <summary>
/// Pins down the contract of <see cref="XpoTableNameResolver.Qualified"/>.
/// The bug this helper fixes: XPO's <c>XPClassInfo.TableName</c>
/// returns either <c>"dbo.t_Asiakas"</c> or <c>"t_Asiakas"</c>
/// depending on the persistent class's mapping; naively wrapping in
/// <c>[dbo].[{name}]</c> double-prefixes the schema-qualified form
/// and SQL Server replies with <c>"Invalid object name
/// 'dbo.dbo.t_Asiakas'"</c>.
/// </summary>
public class XpoTableNameResolverTests
{
    [Fact]
    public void BareTableName_DefaultsToDboSchema()
    {
        Assert.Equal("[dbo].[t_Asiakas]",
            XpoTableNameResolver.Qualified("t_Asiakas"));
    }

    [Fact]
    public void SchemaQualifiedName_BracketsBothPartsIndependently()
    {
        Assert.Equal("[dbo].[t_Asiakas]",
            XpoTableNameResolver.Qualified("dbo.t_Asiakas"));
    }

    [Fact]
    public void NonDboSchema_PreservedVerbatim()
    {
        // xVasu uses 'admin' / 'ASP' / 'tasks' etc. as alternate schemas.
        Assert.Equal("[admin].[t_AdminThing]",
            XpoTableNameResolver.Qualified("admin.t_AdminThing"));
    }

    [Fact]
    public void PreBracketedInput_StripsBracketsBeforeRequalifying()
    {
        // Defensive — never seen this from XPO, but if any future
        // mapping returns pre-quoted output we should still produce a
        // single canonical bracketed form rather than double-quoting.
        Assert.Equal("[dbo].[t_Asiakas]",
            XpoTableNameResolver.Qualified("[t_Asiakas]"));
    }

    [Fact]
    public void WhitespacePadded_IsTrimmed()
    {
        Assert.Equal("[dbo].[t_Asiakas]",
            XpoTableNameResolver.Qualified("  t_Asiakas  "));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NullOrBlank_Throws(string? input)
    {
        // Throws ArgumentNullException for null, ArgumentException for
        // empty/whitespace — both inherit from ArgumentException, but
        // xunit's Assert.Throws<T> requires an exact type match. Use
        // ThrowsAny to accept either subtype.
        Assert.ThrowsAny<ArgumentException>(() =>
            XpoTableNameResolver.Qualified(input!));
    }
}
