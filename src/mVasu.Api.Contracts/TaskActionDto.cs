namespace mVasu.Api.Contracts;

/// <summary>
/// Inline action button that ships with a TaskDto row. The frontend
/// dispatches <see cref="Kind"/> via the <see cref="TaskActionKinds"/>
/// switch — most kinds resolve client-side (tel, sms, navigate),
/// while <c>mark-done</c> and <c>cancel</c> POST to the
/// <c>/api/tasks/:id/*</c> endpoints (BACKEND.md §6).
/// </summary>
public sealed record TaskActionDto(
    /// <summary>One of the values in <see cref="TaskActionKinds"/>.</summary>
    string Kind,
    string Label,
    bool Primary,
    bool Destructive,
    /// <summary>
    /// Hint payload — meaning depends on Kind. For phone/sms it's the
    /// number; for navigate / external it's the URL or route; for
    /// mark-done / cancel it's null.
    /// </summary>
    string? Href);

/// <summary>Stable identifiers for action.kind dispatch — BACKEND.md §6.</summary>
public static class TaskActionKinds
{
    public const string Navigate    = "navigate";
    public const string Phone       = "phone";
    public const string Sms         = "sms";
    public const string MarkDone    = "mark-done";
    public const string Cancel      = "cancel";
    public const string External    = "external";
    public const string CreateOffer = "create-offer";
}
