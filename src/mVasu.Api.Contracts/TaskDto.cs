namespace mVasu.Api.Contracts;

/// <summary>
/// Heterogeneous task surfaced in the Tehtävät tab. Aggregates rows from
/// multiple XAF entity sources (xVasuSecuritySystemUserTask, Tutustumis-
/// kaynti, Yleisesittely, Varausesittely, Tarjous, Sopimus, Saapuneet
/// irtisanomiset, Valokuvaus, Remontti, Liidi, Tiskilista) into a single
/// stream. The mapping rules live in BACKEND.md §2 and EXISTING_ENTITIES.md.
/// </summary>
/// <remarks>
/// <para>The frontend consumes <c>Type</c> / <c>Accent</c> as strings to
/// keep the JSON wire format stable across XAF schema additions; the
/// canonical set lives in <see cref="TaskTypeNames"/> and
/// <see cref="TaskAccents"/>.</para>
/// </remarks>
public sealed record TaskDto(
    string Id,
    /// <summary>One of the values in <see cref="TaskTypeNames"/>.</summary>
    string Type,
    /// <summary>One of the values in <see cref="TaskAccents"/>.</summary>
    string Accent,
    bool Urgent,
    TaskWhenDto When,
    string Title,
    string? Who,
    string? Meta,
    /// <summary>
    /// Optional caption override for the row's type label. Used for
    /// xVasuSecuritySystemUserTask rows where <c>UserTaskType.Name</c>
    /// (e.g. "Hinnoittelu", "Tarkastus") drives the row caption instead
    /// of the static <see cref="Type"/> mapping.
    /// </summary>
    string? TypeLabel,
    IReadOnlyList<TaskActionDto> Actions,
    TaskEntityRefDto EntityRef,
    int? SortOrder);

/// <summary>"When" block: human time + optional secondary note + sort hint.</summary>
public sealed record TaskWhenDto(
    string Time,
    string? Note,
    /// <summary>
    /// Unix-millis timestamp for chronological sort, or 0 for "Heti"
    /// (out-of-band / overdue). Used only on the server / for sorting.
    /// </summary>
    long SortOrder);

/// <summary>
/// Back-reference to the originating XAF entity. Module values track
/// NAVIGATION.md §2 ("asma" / "kire" / "verkkokauppa" / "core").
/// </summary>
public sealed record TaskEntityRefDto(
    string Module,
    string EntityType,
    string Id);

/// <summary>Canonical task-type identifiers — see BACKEND.md §1.</summary>
public static class TaskTypeNames
{
    public const string VisitIntroduction  = "visit-introduction";   // Tutustumiskaynti
    public const string VisitReservation   = "visit-reservation";    // Varausesittely
    public const string OpenHouse          = "open-house";           // Yleisesittely
    public const string SignaturePending   = "signature-pending";    // Tarjous / Sopimus
    public const string InboxTermination   = "inbox-termination";    // Saapunut irtisanomisilmoitus
    public const string InboxSigned        = "inbox-signed";         // Palautunut sähköinen allekirjoitus
    public const string PhotoScheduled     = "photo-scheduled";      // Valokuvaus
    public const string RenovationApproval = "renovation-approval";  // Remontti
    public const string LeadCallback       = "lead-callback";        // Liidi
    public const string DeskListItem       = "desk-list-item";       // Tiskilistan rivi
    public const string GenericTask        = "generic-task";         // xVasuSecuritySystemUserTask
}

/// <summary>Card accent (border-left + primary CTA tint) — see SCREENS.md §01.</summary>
public static class TaskAccents
{
    public const string Navy = "navy";
    public const string Cta  = "cta";
    public const string Info = "info";
    public const string Warn = "warn";
}
