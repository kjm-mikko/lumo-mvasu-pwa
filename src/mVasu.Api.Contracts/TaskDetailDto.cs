namespace mVasu.Api.Contracts;

/// <summary>
/// Full per-task detail returned by <c>GET /api/tasks/:id</c>. Maps to
/// SCREENS.md §04 (Tehtävän tarkka näkymä) and BEHAVIOR.md §5
/// (TaskDetailState shape). Phase 1 returns the same hand-curated mock
/// dataset as <see cref="ITaskQueryService.ListAsync"/>; Phase 2 will
/// resolve the source XAF entity by <see cref="EntityRef"/>.
/// </summary>
public sealed record TaskDetailDto(
    string Id,
    /// <summary>One of <see cref="TaskTypeNames"/>.</summary>
    string Type,
    /// <summary>
    /// Caption shown in the topbar — UserTaskType.Name for
    /// xVasuSecuritySystemUserTask rows, otherwise the localised
    /// label for the static <see cref="Type"/>.
    /// </summary>
    string TypeLabel,
    /// <summary>One of <see cref="TaskAccents"/>.</summary>
    string Accent,
    /// <summary>
    /// Hero pill text — pre-localised by the server (e.g.
    /// "Tänään klo 09:00 · 3 h kuluttua"). Frontend renders verbatim.
    /// </summary>
    string TimeContext,
    string Title,
    string? Subtitle,
    TaskCustomerDto? Customer,
    IReadOnlyList<TaskRowActionDto> Actions,
    TaskNoteDto? Note,
    TaskActionDto PrimaryCta,
    TaskEntityRefDto EntityRef);

/// <summary>Customer block on the detail screen — avatar + name + phone + email.</summary>
public sealed record TaskCustomerDto(
    string Id,
    string Name,
    string Initials,
    string? Phone,
    string? Email);

/// <summary>
/// Row-action shown in the TOIMINNOT list. Distinct from inline-card
/// <see cref="TaskActionDto"/>s because detail rows can require
/// confirmation and never carry a primary flag.
/// </summary>
public sealed record TaskRowActionDto(
    string Id,
    string Label,
    /// <summary>One of <see cref="TaskRowActionKinds"/>.</summary>
    string Kind,
    bool Destructive,
    /// <summary>
    /// Confirmation copy — when null the action runs immediately. The
    /// frontend's reason-picker takes over when <see cref="Kind"/> is
    /// <c>cancel</c> regardless of <see cref="Confirm"/>.
    /// </summary>
    TaskConfirmDto? Confirm,
    string? Href);

/// <summary>Inline confirm dialog copy attached to a row action.</summary>
public sealed record TaskConfirmDto(string Title, string Body);

/// <summary>Inline-editable note. <see cref="UpdatedAt"/> is server-assigned.</summary>
public sealed record TaskNoteDto(
    string Id,
    string Body,
    DateTimeOffset UpdatedAt);

/// <summary>Stable identifiers for the TOIMINNOT row dispatch.</summary>
public static class TaskRowActionKinds
{
    public const string Navigate    = "navigate";
    public const string External    = "external";
    public const string MarkDone    = "mark-done";
    public const string Cancel      = "cancel";
    public const string CreateOffer = "create-offer";
}
