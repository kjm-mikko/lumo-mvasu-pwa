namespace mVasu.Api.Common;

/// <summary>
/// Resolves an XPO <c>XPClassInfo.TableName</c> string into a properly
/// bracketed <c>[schema].[name]</c> SQL identifier suitable for raw
/// SQL Server queries via <c>Session.ExecuteQuery</c>.
/// </summary>
/// <remarks>
/// <para>XPO's <c>TableName</c> can come back in either form depending
/// on the persistent class's <c>[Persistent]</c> attribute and the
/// dictionary's mapping schema:</para>
///
/// <list type="bullet">
///   <item><c>"dbo.t_Asiakas"</c> — schema-qualified (legacy
///   xVasu pattern with explicit schema in the mapping)</item>
///   <item><c>"t_Asiakas"</c> — bare table name (default-schema
///   pattern, dbo is implied)</item>
/// </list>
///
/// <para>If the caller naively wraps either form in
/// <c>[dbo].[{tableName}]</c>, the schema-qualified case becomes
/// <c>[dbo].[dbo.t_Asiakas]</c> which SQL Server parses as an
/// identifier literally named <c>dbo.t_Asiakas</c> in the dbo schema —
/// resulting in <c>"Invalid object name 'dbo.dbo.t_Asiakas'"</c>.</para>
///
/// <para>This helper splits on the last dot, brackets each part
/// independently, and defaults the schema to <c>dbo</c> when the
/// input has no dot. The output is a quoted-identifier string that
/// can be substituted directly into a SQL string template.</para>
/// </remarks>
public static class XpoTableNameResolver
{
    /// <summary>
    /// Returns a fully bracketed <c>[schema].[name]</c> identifier
    /// for the given XPO TableName value. Defaults schema to
    /// <c>dbo</c> when none is present in the input.
    /// </summary>
    public static string Qualified(string xpoTableName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xpoTableName);

        // Strip any quote characters the caller might have already
        // added — defensive in case the XPO mapping ever produces
        // pre-quoted output. The brackets we add below are the
        // single canonical form we want.
        var trimmed = xpoTableName.Trim().Trim('[', ']');

        var dotIdx = trimmed.LastIndexOf('.');
        return dotIdx > 0
            ? $"[{trimmed[..dotIdx]}].[{trimmed[(dotIdx + 1)..]}]"
            : $"[dbo].[{trimmed}]";
    }
}
