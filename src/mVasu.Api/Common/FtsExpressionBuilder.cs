namespace mVasu.Api.Common;

/// <summary>
/// Builds SQL Server <c>CONTAINS</c> expressions from raw user input.
/// All FTS-using services in mVasu.Api share the same prefix-wildcard
/// + AND-join semantics and the same reserved-word / character
/// sanitisation so multi-word queries narrow the result set rather
/// than widening it.
/// </summary>
/// <remarks>
/// <para>The output is the second argument of CONTAINS — wrapped in
/// double-quoted prefix terms joined with AND, e.g. <c>"matti koivu"
/// → "matti*" AND "koivu*"</c>.</para>
///
/// <para>Returns <c>null</c> when nothing usable remains after
/// sanitisation (only operator words, single chars, blanks etc.) so
/// the caller knows to skip the FTS round-trip and short-circuit to
/// "no matches".</para>
/// </remarks>
public static class FtsExpressionBuilder
{
    /// <summary>
    /// Translates the user's raw search input into a SQL Server
    /// <c>CONTAINS</c> expression. Each whitespace-delimited token is
    /// sanitised, wrapped in <c>"word*"</c> for prefix matching and
    /// AND-joined.
    /// </summary>
    public static string? Build(string searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm)) return null;

        var tokens = searchTerm.Split(
            new[] { ' ', '\t', '\n', '\r' },
            StringSplitOptions.RemoveEmptyEntries);

        var clauses = new List<string>(tokens.Length);
        foreach (var raw in tokens)
        {
            // Strip the few characters that have meta meaning in
            // CONTAINS expressions (quotes, square brackets) and the
            // outer whitespace. Leaves accented characters intact —
            // the catalogs are built with the Finnish word breaker,
            // ä/ö/å are first-class.
            var sanitised = raw
                .Replace("\"", string.Empty)
                .Replace("[", string.Empty)
                .Replace("]", string.Empty)
                .Trim();

            if (sanitised.Length < 2) continue;
            if (IsReservedWord(sanitised)) continue;

            clauses.Add("\"" + sanitised + "*\"");
        }

        return clauses.Count == 0 ? null : string.Join(" AND ", clauses);
    }

    private static bool IsReservedWord(string word) =>
        string.Equals(word, "AND",  StringComparison.OrdinalIgnoreCase) ||
        string.Equals(word, "OR",   StringComparison.OrdinalIgnoreCase) ||
        string.Equals(word, "NOT",  StringComparison.OrdinalIgnoreCase) ||
        string.Equals(word, "NEAR", StringComparison.OrdinalIgnoreCase);
}
