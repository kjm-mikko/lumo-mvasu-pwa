using System.Globalization;
using System.Security.Claims;
using DevExpress.Data.Filtering;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Xpo;
using DevExpress.Xpo;
using mVasu.Api.Authentication;
using mVasu.Api.Contracts;
using xVasu.Data.Asma;
using xVasu.Data.Kire;
using xVasu.Data.Security;
using XpoInspection = xVasu.Data.DirectRent.DirectRentalCustomerInspection;
using XpoOffer = xVasu.Data.Varaus.SopimusVaraus;
using XpoPriority = xVasu.Data.Security.Priority;
using XpoShowing = xVasu.Data.Kire.HuoneistoEsittelyTiedot;
using XpoSopimus = xVasu.Data.Vuha.Sopimus;
using XpoTask = xVasu.Data.Security.xVasuSecuritySystemUserTask;
using XpoTaskStatus = xVasu.Data.Security.TaskStatus;

namespace mVasu.Api.Tasks;

/// <summary>
/// Aggregates per-user task rows across multiple XAF entity sources.
/// Currently covered:
/// <list type="bullet">
///   <item>A1 — <c>xVasuSecuritySystemUserTask</c> as <see cref="TaskTypeNames.GenericTask"/>.</item>
///   <item>A2 — <c>DirectRentalCustomerInspection</c> as <see cref="TaskTypeNames.VisitIntroduction"/>.</item>
///   <item>A3 — <c>HuoneistoEsittelyTiedot</c> as <see cref="TaskTypeNames.VisitReservation"/>
///         or <see cref="TaskTypeNames.OpenHouse"/>, discriminated by
///         <c>EsittelyTyyppi.EsittelyNimi</c>.</item>
///   <item>A4 — <c>SopimusVaraus</c> as <see cref="TaskTypeNames.SignaturePending"/>.
///         The entity has no per-user FK so visibility falls back to the
///         AlueToimisto scope (<c>Talousyksikkö.Aluetunnus</c> ∈ user's
///         <c>AlueToimistot</c> list, mirroring the Tiskilista pattern).</item>
/// </list>
/// Pending in A5-A8: Saapuneet irtisanomiset / Valokuvaus / Remontti /
/// Liidi / Tiskilista. See
/// design/handoff_navigation/v5/.../BACKEND.md §2 for the canonical mapping.
/// </summary>
public sealed class TaskQueryService(
    IObjectSpaceProvider objectSpaceProvider,
    ILogger<TaskQueryService> logger) : ITaskQueryService
{
    private static readonly TimeZoneInfo HelsinkiTz = ResolveHelsinkiTimeZone();
    private static readonly CultureInfo FiCulture = CultureInfo.GetCultureInfo("fi-FI");

    public Task<TasksResponseDto> ListAsync(
        ClaimsPrincipal principal,
        TaskQueryParameters query,
        CancellationToken cancellationToken = default)
    {
        var (user, os) = ResolveUser(principal);
        if (user is null || os is null)
        {
            return Task.FromResult(EmptyResponse());
        }

        try
        {
            var nowHelsinki = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, HelsinkiTz);
            var today = DateOnly.FromDateTime(nowHelsinki);
            var session = ((XPObjectSpace)os).Session;

            // Date bounds: only apply when caller explicitly passes them. Defaults
            // intentionally have no lower bound so overdue rows surface in the
            // Today bucket as "Heti / myöhässä N pv". Default upper bound is far
            // enough to surface near-term planning without dragging in 5-year
            // milestones that hide today's work.
            var fromDate = query.From;
            var toDate = query.To ?? today.AddDays(90);

            var tasks = new List<TaskDto>();
            // Per-source isolation: one broken source (XPO load failure, missing
            // FK target, schema drift) must not kill the whole endpoint.
            if (TypeAllowed(query, TaskTypeNames.GenericTask))
            {
                SafelyAdd(tasks, "GenericTask",
                    () => QueryGenericTasks(session, user.Oid, fromDate, toDate, query.UrgentOnly, today));
            }
            if (TypeAllowed(query, TaskTypeNames.VisitIntroduction))
            {
                SafelyAdd(tasks, "VisitIntroduction",
                    () => QueryVisitIntroductions(session, user.Oid, fromDate, toDate, query.UrgentOnly, today, nowHelsinki));
            }
            if (TypeAllowed(query, TaskTypeNames.VisitReservation) || TypeAllowed(query, TaskTypeNames.OpenHouse))
            {
                SafelyAdd(tasks, "Showing",
                    () => QueryShowings(session, user.Oid, fromDate, toDate, query, today, nowHelsinki));
            }
            if (TypeAllowed(query, TaskTypeNames.SignaturePending))
            {
                SafelyAdd(tasks, "SignaturePending",
                    () => QuerySignaturePending(session, user, today, nowHelsinki));
            }

            var groups = BucketByDay(tasks, today);
            var total = groups.Sum(g => g.Tasks.Count);
            return Task.FromResult(new TasksResponseDto(groups, total, DateTimeOffset.UtcNow));
        }
        finally
        {
            os.Dispose();
        }
    }

    public Task<TaskDetailDto?> GetAsync(
        ClaimsPrincipal principal,
        string id,
        CancellationToken cancellationToken = default)
    {
        var (user, os) = ResolveUser(principal);
        if (user is null || os is null)
        {
            return Task.FromResult<TaskDetailDto?>(null);
        }

        try
        {
            var nowHelsinki = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, HelsinkiTz);
            var today = DateOnly.FromDateTime(nowHelsinki);

            // Showing keys are Int32; tasks and inspections share Guid.
            if (Guid.TryParse(id, out var oid))
            {
                var task = os.GetObjectByKey<XpoTask>(oid);
                if (task is not null)
                {
                    if (!IsGenericTaskVisibleTo(task, user.Oid))
                    {
                        logger.LogInformation("Task {Oid} access denied for user {UserOid}", oid, user.Oid);
                        return Task.FromResult<TaskDetailDto?>(null);
                    }
                    return Task.FromResult<TaskDetailDto?>(MapGenericTaskDetail(task, today));
                }

                var inspection = os.GetObjectByKey<XpoInspection>(oid);
                if (inspection is not null)
                {
                    if (!IsInspectionVisibleTo(inspection, user.Oid))
                    {
                        logger.LogInformation("Inspection {Oid} access denied for user {UserOid}", oid, user.Oid);
                        return Task.FromResult<TaskDetailDto?>(null);
                    }
                    return Task.FromResult<TaskDetailDto?>(MapInspectionDetail(inspection, today, nowHelsinki));
                }
            }
            else if (int.TryParse(id, System.Globalization.NumberStyles.Integer, CultureInfo.InvariantCulture, out var intId))
            {
                var showing = os.GetObjectByKey<XpoShowing>(intId);
                if (showing is not null)
                {
                    if (!IsShowingVisibleTo(showing, user.Oid))
                    {
                        logger.LogInformation("Showing {Id} access denied for user {UserOid}", intId, user.Oid);
                        return Task.FromResult<TaskDetailDto?>(null);
                    }
                    return Task.FromResult<TaskDetailDto?>(MapShowingDetail(showing, today, nowHelsinki));
                }

                // SopimusVaraus shares the int key namespace with HuoneistoEsittelyTiedot;
                // its visibility is AlueToimisto-scoped, not per-user.
                var offer = os.GetObjectByKey<XpoOffer>(intId);
                if (offer is not null)
                {
                    var offerScope = UserAreaScope.Resolve(user);
                    if (!IsOfferVisibleTo(offer, offerScope))
                    {
                        logger.LogInformation("Offer {Id} access denied for user {UserOid}", intId, user.Oid);
                        return Task.FromResult<TaskDetailDto?>(null);
                    }
                    return Task.FromResult<TaskDetailDto?>(MapOfferDetail(offer, nowHelsinki));
                }
            }

            return Task.FromResult<TaskDetailDto?>(null);
        }
        finally
        {
            os.Dispose();
        }
    }

    private (xVasuSecuritySystemUser? user, IObjectSpace? os) ResolveUser(ClaimsPrincipal principal)
    {
        var os = objectSpaceProvider.CreateObjectSpace();

        var email = EmailResolver.ResolveEmail(principal);
        if (string.IsNullOrEmpty(email))
        {
            logger.LogWarning("Tasks request rejected — principal had no resolvable email");
            os.Dispose();
            return (null, null);
        }

        var variants = EmailResolver.BuildEmailVariants(email);
        var resolved = os.FindObject<xVasuSecuritySystemUser>(EmailResolver.BuildUserCriteria(variants, email));
        if (resolved is null)
        {
            logger.LogInformation("Tasks request rejected — user {Email} not provisioned", email);
            os.Dispose();
            return (null, null);
        }

        return (resolved, os);
    }

    /// <summary>
    /// When the caller does not pass an explicit <c>?types=</c> filter, the
    /// list defaults to these three sources only — that is the agreed
    /// MVP-1 surface (Tutustumiskäynnit, Varausesittelyt, Käyttäjän tehtävät).
    /// Callers can still opt-in to OpenHouse or SignaturePending by passing
    /// the type explicitly.
    /// </summary>
    private static readonly IReadOnlyList<string> DefaultTypeAllowList =
    [
        TaskTypeNames.GenericTask,
        TaskTypeNames.VisitIntroduction,
        TaskTypeNames.VisitReservation,
    ];

    private static bool TypeAllowed(TaskQueryParameters query, string type)
    {
        if (query.Types is { Count: > 0 })
        {
            return query.Types.Contains(type);
        }
        return DefaultTypeAllowList.Contains(type);
    }

    private void SafelyAdd(List<TaskDto> tasks, string sourceName, Func<IEnumerable<TaskDto>> producer)
    {
        try
        {
            tasks.AddRange(producer());
        }
        catch (Exception ex)
        {
            // Log and skip — a broken source must not poison the whole endpoint.
            // Likely culprits: missing FK targets (referential-integrity drift),
            // schema additions in the xVasu module that don't match the live DB.
            logger.LogError(ex, "Task source {Source} failed; skipping", sourceName);
        }
    }

    // -- Generic task source (xVasuSecuritySystemUserTask) ---------------------

    private static IEnumerable<TaskDto> QueryGenericTasks(
        Session session, Guid userOid, DateOnly? from, DateOnly to, bool urgentOnly, DateOnly today)
    {
        var criteria = BuildGenericTaskCriteria(userOid, from, to, urgentOnly);
        var collection = new XPCollection<XpoTask>(session, criteria);
        collection.Sorting.Add(new SortProperty("DueDate", DevExpress.Xpo.DB.SortingDirection.Ascending));
        return collection.Select(t => MapGenericTaskCard(t, today)).ToList();
    }

    private static CriteriaOperator BuildGenericTaskCriteria(Guid userOid, DateOnly? from, DateOnly to, bool urgentOnly)
    {
        var operands = new List<CriteriaOperator>
        {
            // Visibility: Owner OR AssignedTo OR additional-user link
            CriteriaOperator.Or(
                new BinaryOperator("Owner.Oid", userOid),
                new BinaryOperator("AssignedTo.Oid", userOid),
                new ContainsOperator("xVasuUserTaskAdditionalUsers", new BinaryOperator("UserID.Oid", userOid))),

            // Hide finished work
            new BinaryOperator("Status", XpoTaskStatus.Completed, BinaryOperatorType.NotEqual),

            // Upper bound only — overdue rows always surface as "Heti".
            new BinaryOperator("DueDate", to.ToDateTime(new TimeOnly(23, 59, 59)), BinaryOperatorType.LessOrEqual),
        };

        if (from is not null)
        {
            operands.Add(new BinaryOperator("DueDate", from.Value.ToDateTime(TimeOnly.MinValue), BinaryOperatorType.GreaterOrEqual));
        }

        if (urgentOnly)
        {
            operands.Add(new BinaryOperator("Priority", XpoPriority.High, BinaryOperatorType.Equal));
        }

        return CriteriaOperator.And(operands);
    }

    private static bool IsGenericTaskVisibleTo(XpoTask t, Guid userOid)
    {
        if (t.Owner?.Oid == userOid) return true;
        if (t.AssignedTo?.Oid == userOid) return true;
        return t.xVasuUserTaskAdditionalUsers.OfType<xVasuUserTaskAdditionalUser>()
            .Any(au => au.UserID?.Oid == userOid);
    }

    private static TaskDto MapGenericTaskCard(XpoTask t, DateOnly today)
    {
        var due = ExtractDueDate(t);
        var when = FormatWhen(due, today);
        var (accent, urgent) = DeriveGenericAccent(t.Priority, due, today);
        var typeLabel = t.UserTaskType?.Name;
        var who = ResolveGenericWho(t);
        var meta = ResolveGenericMeta(t);

        return new TaskDto(
            Id: t.Oid.ToString(),
            Type: TaskTypeNames.GenericTask,
            Accent: accent,
            Urgent: urgent,
            When: when,
            Title: string.IsNullOrWhiteSpace(t.Subject) ? "Tehtävä" : t.Subject,
            Who: who,
            Meta: meta,
            TypeLabel: typeLabel,
            Actions:
            [
                new TaskActionDto(TaskActionKinds.Navigate, "Avaa tehtävä", Primary: true, Destructive: false, Href: null),
                new TaskActionDto(TaskActionKinds.MarkDone, "Merkitse tehdyksi", Primary: false, Destructive: false, Href: null),
            ],
            EntityRef: new TaskEntityRefDto("core", "xVasuSecuritySystemUserTask", t.Oid.ToString()),
            SortOrder: when.SortOrder == 0 ? 0 : (int?)null);
    }

    private static TaskDetailDto MapGenericTaskDetail(XpoTask t, DateOnly today)
    {
        var due = ExtractDueDate(t);
        var when = FormatWhen(due, today);
        var (accent, _) = DeriveGenericAccent(t.Priority, due, today);
        var typeLabel = t.UserTaskType?.Name ?? "Tehtävä";
        var meta = ResolveGenericMeta(t);

        var subtitleParts = new List<string>(3);
        if (!string.IsNullOrWhiteSpace(meta)) subtitleParts.Add(meta!);
        subtitleParts.Add($"Status {t.Status}");
        subtitleParts.Add($"Priority {t.Priority}");

        var timeContext = string.IsNullOrEmpty(when.Note)
            ? when.Time
            : $"{when.Time} · {when.Note}";

        var actions = new List<TaskRowActionDto>
        {
            new("mark-done", "Merkitse tehdyksi",
                TaskRowActionKinds.MarkDone, Destructive: false,
                Confirm: new TaskConfirmDto("Vahvistus", "Tehtävä merkitään tehdyksi. Jatka?"),
                Href: null),
            new("cancel", "Peruuta",
                TaskRowActionKinds.Cancel, Destructive: true,
                Confirm: new TaskConfirmDto("Peruuta",
                    "Peruutetaanko tehtävä? Toiminto kirjataan auditiin."),
                Href: null),
        };

        var note = string.IsNullOrWhiteSpace(t.Description)
            ? null
            : new TaskNoteDto($"note-{t.Oid}", t.Description, DateTimeOffset.UtcNow);

        return new TaskDetailDto(
            Id: t.Oid.ToString(),
            Type: TaskTypeNames.GenericTask,
            TypeLabel: typeLabel,
            Accent: accent,
            TimeContext: timeContext,
            Title: string.IsNullOrWhiteSpace(t.Subject) ? "Tehtävä" : t.Subject,
            Subtitle: string.Join(" · ", subtitleParts),
            Customer: null,
            Actions: actions,
            Note: note,
            PrimaryCta: new TaskActionDto(
                TaskActionKinds.Navigate, "Avaa kohde", Primary: true, Destructive: false, Href: null),
            EntityRef: new TaskEntityRefDto("core", "xVasuSecuritySystemUserTask", t.Oid.ToString()));
    }

    private static DateTime? ExtractDueDate(XpoTask t)
    {
        var due = t.DueDate;
        if (due < new DateTime(1900, 1, 1)) return null;
        return due;
    }

    private static string? ResolveGenericWho(XpoTask t)
    {
        var assignee = t.AssignedTo?.Kokonimi;
        var owner = t.Owner?.Kokonimi;
        if (!string.IsNullOrWhiteSpace(assignee)) return assignee;
        if (!string.IsNullOrWhiteSpace(owner)) return owner;
        return null;
    }

    private static string? ResolveGenericMeta(XpoTask t)
    {
        var huoneisto = FormatHuoneisto(t.Huoneisto);
        if (huoneisto is not null) return huoneisto;

        var sopimus = FormatSopimus(t.Sopimus);
        if (sopimus is not null) return sopimus;

        if (t.Kohde is not null && !string.IsNullOrWhiteSpace(t.Kohde.Header)) return t.Kohde.Header;
        return null;
    }

    private static string? FormatHuoneisto(Huoneisto? h)
    {
        if (h is null) return null;
        if (!string.IsNullOrWhiteSpace(h.Header)) return h.Header;
        if (!string.IsNullOrWhiteSpace(h.Osoite)) return h.Osoite;
        return null;
    }

    private static string? FormatSopimus(XpoSopimus? s)
    {
        if (s is null) return null;
        if (!string.IsNullOrWhiteSpace(s.MobileHeaderAlias)) return s.MobileHeaderAlias;
        if (!string.IsNullOrWhiteSpace(s.Header)) return s.Header;
        return null;
    }

    private static (string accent, bool urgent) DeriveGenericAccent(XpoPriority priority, DateTime? due, DateOnly today)
    {
        var overdue = due is { } d && DateOnly.FromDateTime(d) < today;
        if (priority == XpoPriority.High || overdue) return (TaskAccents.Warn, true);
        if (priority == XpoPriority.Low) return (TaskAccents.Info, false);
        return (TaskAccents.Navy, false);
    }

    // -- Visit introduction source (DirectRentalCustomerInspection) ------------

    private static IEnumerable<TaskDto> QueryVisitIntroductions(
        Session session, Guid userOid, DateOnly? from, DateOnly to, bool urgentOnly,
        DateOnly today, DateTime nowHelsinki)
    {
        var criteria = BuildInspectionCriteria(userOid, from, to, urgentOnly, nowHelsinki);
        var collection = new XPCollection<XpoInspection>(session, criteria);
        collection.Sorting.Add(new SortProperty("StartedOn", DevExpress.Xpo.DB.SortingDirection.Ascending));
        return collection.Select(i => MapInspectionCard(i, today, nowHelsinki)).ToList();
    }

    private static CriteriaOperator BuildInspectionCriteria(
        Guid userOid, DateOnly? from, DateOnly to, bool urgentOnly, DateTime nowHelsinki)
    {
        var operands = new List<CriteriaOperator>
        {
            // Visibility: the inspector or the row creator
            CriteriaOperator.Or(
                new BinaryOperator("Employee.Oid", userOid),
                new BinaryOperator("Owner.Oid", userOid)),

            new BinaryOperator("IsCancelled", false),
            new BinaryOperator("IsHandled", false),

            // Upper bound only — past unhandled inspections still surface as urgent.
            new BinaryOperator("StartedOn", to.ToDateTime(new TimeOnly(23, 59, 59)), BinaryOperatorType.LessOrEqual),
        };

        if (from is not null)
        {
            operands.Add(new BinaryOperator("StartedOn", from.Value.ToDateTime(TimeOnly.MinValue), BinaryOperatorType.GreaterOrEqual));
        }

        if (urgentOnly)
        {
            // BACKEND.md §2.1 — urgent if the appointment starts inside the next 2 hours.
            var twoHoursOut = nowHelsinki.AddHours(2);
            operands.Add(new BinaryOperator("StartedOn", twoHoursOut, BinaryOperatorType.LessOrEqual));
        }

        return CriteriaOperator.And(operands);
    }

    private static bool IsInspectionVisibleTo(XpoInspection i, Guid userOid) =>
        i.Employee?.Oid == userOid || i.Owner?.Oid == userOid;

    private static TaskDto MapInspectionCard(XpoInspection i, DateOnly today, DateTime nowHelsinki)
    {
        var when = FormatWhen(i.StartedOn, today);
        var urgent = IsInspectionUrgent(i.StartedOn, nowHelsinki);
        if (urgent && when.Note is null)
        {
            when = when with { Note = "alkaa pian" };
        }

        var (title, who, customerPhone) = ResolveInspectionParties(i);
        var meta = BuildInspectionMeta(i.Duration);

        var actions = new List<TaskActionDto>
        {
            new(TaskActionKinds.Navigate, "Avaa kohde", Primary: true, Destructive: false, Href: null),
        };
        if (!string.IsNullOrWhiteSpace(customerPhone))
        {
            actions.Add(new TaskActionDto(TaskActionKinds.Phone, "Soita", Primary: false, Destructive: false, $"tel:{customerPhone}"));
            actions.Add(new TaskActionDto(TaskActionKinds.Sms, "Viesti", Primary: false, Destructive: false, $"sms:{customerPhone}"));
        }

        return new TaskDto(
            Id: i.OID.ToString(),
            Type: TaskTypeNames.VisitIntroduction,
            Accent: TaskAccents.Navy,
            Urgent: urgent,
            When: when,
            Title: title,
            Who: who,
            Meta: meta,
            TypeLabel: null,
            Actions: actions,
            EntityRef: new TaskEntityRefDto("core", "Tutustumiskaynti", i.OID.ToString()),
            SortOrder: when.SortOrder == 0 ? 0 : (int?)null);
    }

    private static TaskDetailDto MapInspectionDetail(XpoInspection i, DateOnly today, DateTime nowHelsinki)
    {
        var when = FormatWhen(i.StartedOn, today);
        var (title, who, customerPhone) = ResolveInspectionParties(i);
        var meta = BuildInspectionMeta(i.Duration);

        var customer = i.Asiakas is null
            ? null
            : new TaskCustomerDto(
                Id: i.Asiakas.AsiakasNumero.ToString(),
                Name: i.Asiakas.Kokonimi ?? i.Asiakas.Header ?? "Asiakas",
                Initials: InitialsFor(i.Asiakas.Kokonimi ?? i.Asiakas.Header ?? string.Empty),
                Phone: string.IsNullOrWhiteSpace(i.Asiakas.Gsm) ? null : i.Asiakas.Gsm,
                Email: string.IsNullOrWhiteSpace(i.Asiakas.Email) ? null : i.Asiakas.Email);

        var subtitleParts = new List<string>();
        if (!string.IsNullOrWhiteSpace(meta)) subtitleParts.Add(meta!);
        var huoneistoText = FormatHuoneisto(i.Huoneisto);
        // Avoid duplicating the apartment when the title already shows it.
        if (!string.IsNullOrWhiteSpace(huoneistoText) && huoneistoText != title)
        {
            subtitleParts.Add(huoneistoText!);
        }

        var timeContext = string.IsNullOrEmpty(when.Note)
            ? when.Time
            : $"{when.Time} · {when.Note}";

        var actions = new List<TaskRowActionDto>
        {
            new("navigate-unit", "Avaa kohde Lumo Verkossa",
                TaskRowActionKinds.External, Destructive: false, Confirm: null,
                Href: "https://www.lumo.fi/"),
            new("create-offer", "Tee tarjous tästä",
                TaskRowActionKinds.CreateOffer, Destructive: false, Confirm: null, Href: null),
            new("mark-done", "Merkitse pidetyksi",
                TaskRowActionKinds.MarkDone, Destructive: false,
                Confirm: new TaskConfirmDto("Käynti pidetty?", "Käynti merkitään pidetyksi."),
                Href: null),
            new("cancel", "Peruuta",
                TaskRowActionKinds.Cancel, Destructive: true,
                Confirm: new TaskConfirmDto("Peruuta",
                    "Peruutetaanko käynti? Toiminto kirjataan auditiin."),
                Href: null),
        };

        var note = string.IsNullOrWhiteSpace(i.Description)
            ? null
            : new TaskNoteDto($"note-{i.OID}", i.Description, DateTimeOffset.UtcNow);

        return new TaskDetailDto(
            Id: i.OID.ToString(),
            Type: TaskTypeNames.VisitIntroduction,
            TypeLabel: "Tutustumiskäynti",
            Accent: TaskAccents.Navy,
            TimeContext: timeContext,
            Title: title,
            Subtitle: subtitleParts.Count == 0 ? null : string.Join(" · ", subtitleParts),
            Customer: customer,
            Actions: actions,
            Note: note,
            PrimaryCta: new TaskActionDto(
                TaskActionKinds.Navigate, "Avaa kohde", Primary: true, Destructive: false, Href: null),
            EntityRef: new TaskEntityRefDto("core", "Tutustumiskaynti", i.OID.ToString()));
    }

    private static (string title, string? who, string? customerPhone) ResolveInspectionParties(XpoInspection i)
    {
        var huoneistoText = FormatHuoneisto(i.Huoneisto);
        var fallbackTitle = string.IsNullOrWhiteSpace(i.Subject) ? "Tutustumiskäynti" : i.Subject!;
        var title = huoneistoText ?? fallbackTitle;

        var customerName = i.Asiakas?.Kokonimi ?? i.Asiakas?.Header;
        var customerPhone = i.Asiakas?.Gsm;
        var who = customerName is null
            ? null
            : (string.IsNullOrWhiteSpace(customerPhone) ? customerName : $"{customerName} · {customerPhone}");

        return (title, who, string.IsNullOrWhiteSpace(customerPhone) ? null : customerPhone);
    }

    private static string? BuildInspectionMeta(TimeSpan duration)
    {
        var minutes = (int)duration.TotalMinutes;
        return minutes > 0 ? $"Kesto {minutes} min" : null;
    }

    private static bool IsInspectionUrgent(DateTime startedOn, DateTime nowHelsinki) =>
        startedOn >= nowHelsinki && startedOn <= nowHelsinki.AddHours(2);

    private static string InitialsFor(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "··";
        var parts = name.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0) return "··";
        if (parts.Length == 1) return parts[0][..Math.Min(2, parts[0].Length)].ToUpperInvariant();
        return $"{parts[0][0]}{parts[^1][0]}".ToUpperInvariant();
    }

    // -- Showing source (HuoneistoEsittelyTiedot — Yleisesittely + Varausesittely) -

    private static IEnumerable<TaskDto> QueryShowings(
        Session session, Guid userOid, DateOnly? from, DateOnly to,
        TaskQueryParameters query, DateOnly today, DateTime nowHelsinki)
    {
        var criteria = BuildShowingCriteria(userOid, from, to, query.UrgentOnly, nowHelsinki);
        var collection = new XPCollection<XpoShowing>(session, criteria);
        collection.Sorting.Add(new SortProperty("EsittelyAika", DevExpress.Xpo.DB.SortingDirection.Ascending));

        var result = new List<TaskDto>();
        foreach (var showing in collection)
        {
            var type = ResolveShowingType(showing);
            if (type is null) continue;                 // unknown discriminator → skip
            if (!TypeAllowed(query, type)) continue;    // honour query.Types filter
            result.Add(MapShowingCard(showing, type, today, nowHelsinki));
        }
        return result;
    }

    private static CriteriaOperator BuildShowingCriteria(
        Guid userOid, DateOnly? from, DateOnly to, bool urgentOnly, DateTime nowHelsinki)
    {
        var operands = new List<CriteriaOperator>
        {
            // Visibility: anyone owning the showing — inspector, creator, or booker.
            CriteriaOperator.Or(
                new BinaryOperator("Employee.Oid", userOid),
                new BinaryOperator("Owner.Oid", userOid),
                new BinaryOperator("Varaaja.Oid", userOid)),

            new BinaryOperator("EsittelyCancelled", false),
            new BinaryOperator("EsittelyHandled", false),
            new BinaryOperator("SystemCancelled", false),

            new BinaryOperator("EsittelyAika", to.ToDateTime(new TimeOnly(23, 59, 59)), BinaryOperatorType.LessOrEqual),
        };

        if (from is not null)
        {
            operands.Add(new BinaryOperator("EsittelyAika", from.Value.ToDateTime(TimeOnly.MinValue), BinaryOperatorType.GreaterOrEqual));
        }

        if (urgentOnly)
        {
            // Same urgency window as inspection — appointments inside the next 2 hours.
            operands.Add(new BinaryOperator("EsittelyAika", nowHelsinki.AddHours(2), BinaryOperatorType.LessOrEqual));
        }

        return CriteriaOperator.And(operands);
    }

    private static bool IsShowingVisibleTo(XpoShowing s, Guid userOid) =>
        s.Employee?.Oid == userOid || s.Owner?.Oid == userOid || s.Varaaja?.Oid == userOid;

    private static string? ResolveShowingType(XpoShowing s)
    {
        // Discriminator is FK to a lookup table (HuoneistoEsittely.EsittelyNimi),
        // not an enum, so match on name fragments rather than fixed integers.
        var nimi = s.EsittelyTyyppi?.EsittelyNimi;
        if (string.IsNullOrWhiteSpace(nimi)) return null;
        if (nimi.Contains("Varaus", StringComparison.OrdinalIgnoreCase))
        {
            return TaskTypeNames.VisitReservation;
        }
        if (nimi.Contains("Yleis", StringComparison.OrdinalIgnoreCase))
        {
            return TaskTypeNames.OpenHouse;
        }
        return null;
    }

    private static TaskDto MapShowingCard(XpoShowing s, string type, DateOnly today, DateTime nowHelsinki)
    {
        var when = FormatWhen(s.EsittelyAika, today);
        var urgent = IsInspectionUrgent(s.EsittelyAika, nowHelsinki);
        if (urgent && when.Note is null)
        {
            when = when with { Note = "alkaa pian" };
        }

        var title = FormatHuoneisto(s.Huoneisto)
            ?? (string.IsNullOrWhiteSpace(s.Subject) ? DefaultShowingTitle(type) : s.Subject!);

        var customerName = s.Asiakas?.Kokonimi ?? s.Asiakas?.Header;
        var customerPhone = string.IsNullOrWhiteSpace(s.Asiakas?.Gsm) ? null : s.Asiakas!.Gsm;
        var who = customerName is null
            ? null
            : (customerPhone is null ? customerName : $"{customerName} · {customerPhone}");

        // VisitorAmount is filled in after the fact (handled rows only) so
        // it is always 0 here — don't show it pre-event.
        var meta = s.EsittelyKesto.TotalMinutes > 0
            ? $"Kesto {(int)s.EsittelyKesto.TotalMinutes} min"
            : null;

        var actions = new List<TaskActionDto>
        {
            new(TaskActionKinds.Navigate, "Avaa kohde", Primary: true, Destructive: false, Href: null),
        };
        if (customerPhone is not null)
        {
            actions.Add(new TaskActionDto(TaskActionKinds.Phone, "Soita", Primary: false, Destructive: false, $"tel:{customerPhone}"));
            actions.Add(new TaskActionDto(TaskActionKinds.Sms, "Viesti", Primary: false, Destructive: false, $"sms:{customerPhone}"));
        }

        return new TaskDto(
            Id: s.HuoneistoEsittelyId.ToString(CultureInfo.InvariantCulture),
            Type: type,
            Accent: TaskAccents.Navy,
            Urgent: urgent,
            When: when,
            Title: title,
            Who: who,
            Meta: meta,
            TypeLabel: null,
            Actions: actions,
            EntityRef: new TaskEntityRefDto("core", ShowingEntityType(type), s.HuoneistoEsittelyId.ToString(CultureInfo.InvariantCulture)),
            SortOrder: when.SortOrder == 0 ? 0 : (int?)null);
    }

    private static TaskDetailDto MapShowingDetail(XpoShowing s, DateOnly today, DateTime nowHelsinki)
    {
        var type = ResolveShowingType(s) ?? TaskTypeNames.OpenHouse;
        var when = FormatWhen(s.EsittelyAika, today);
        var title = FormatHuoneisto(s.Huoneisto)
            ?? (string.IsNullOrWhiteSpace(s.Subject) ? DefaultShowingTitle(type) : s.Subject!);

        var customer = s.Asiakas is null
            ? null
            : new TaskCustomerDto(
                Id: s.Asiakas.AsiakasNumero.ToString(CultureInfo.InvariantCulture),
                Name: s.Asiakas.Kokonimi ?? s.Asiakas.Header ?? "Asiakas",
                Initials: InitialsFor(s.Asiakas.Kokonimi ?? s.Asiakas.Header ?? string.Empty),
                Phone: string.IsNullOrWhiteSpace(s.Asiakas.Gsm) ? null : s.Asiakas.Gsm,
                Email: string.IsNullOrWhiteSpace(s.Asiakas.Email) ? null : s.Asiakas.Email);

        var subtitleParts = new List<string>();
        if (s.EsittelyKesto.TotalMinutes > 0)
        {
            subtitleParts.Add($"Kesto {(int)s.EsittelyKesto.TotalMinutes} min");
        }
        if (!string.IsNullOrWhiteSpace(s.EsittelijaNimi))
        {
            subtitleParts.Add($"Esittelijä {s.EsittelijaNimi}");
        }

        var timeContext = string.IsNullOrEmpty(when.Note)
            ? when.Time
            : $"{when.Time} · {when.Note}";

        var actions = new List<TaskRowActionDto>
        {
            new("navigate-unit", "Avaa kohde Lumo Verkossa",
                TaskRowActionKinds.External, Destructive: false, Confirm: null,
                Href: "https://www.lumo.fi/"),
            new("create-offer", "Tee tarjous tästä",
                TaskRowActionKinds.CreateOffer, Destructive: false, Confirm: null, Href: null),
            new("mark-done", "Merkitse pidetyksi",
                TaskRowActionKinds.MarkDone, Destructive: false,
                Confirm: new TaskConfirmDto("Esittely pidetty?", "Esittely merkitään pidetyksi."),
                Href: null),
            new("cancel", "Peruuta",
                TaskRowActionKinds.Cancel, Destructive: true,
                Confirm: new TaskConfirmDto("Peruuta",
                    "Peruutetaanko esittely? Toiminto kirjataan auditiin."),
                Href: null),
        };

        var note = string.IsNullOrWhiteSpace(s.EsittelyMemo)
            ? null
            : new TaskNoteDto($"note-{s.HuoneistoEsittelyId}", s.EsittelyMemo, DateTimeOffset.UtcNow);

        var idStr = s.HuoneistoEsittelyId.ToString(CultureInfo.InvariantCulture);
        return new TaskDetailDto(
            Id: idStr,
            Type: type,
            TypeLabel: ShowingTypeLabel(type),
            Accent: TaskAccents.Navy,
            TimeContext: timeContext,
            Title: title,
            Subtitle: subtitleParts.Count == 0 ? null : string.Join(" · ", subtitleParts),
            Customer: customer,
            Actions: actions,
            Note: note,
            PrimaryCta: new TaskActionDto(
                TaskActionKinds.Navigate, "Avaa kohde", Primary: true, Destructive: false, Href: null),
            EntityRef: new TaskEntityRefDto("core", ShowingEntityType(type), idStr));
    }

    private static string DefaultShowingTitle(string type) => type switch
    {
        TaskTypeNames.VisitReservation => "Varausesittely",
        TaskTypeNames.OpenHouse => "Yleisesittely",
        _ => "Esittely",
    };

    private static string ShowingTypeLabel(string type) => type switch
    {
        TaskTypeNames.VisitReservation => "Varausesittely",
        TaskTypeNames.OpenHouse => "Yleisesittely",
        _ => "Esittely",
    };

    private static string ShowingEntityType(string type) => type switch
    {
        TaskTypeNames.VisitReservation => "Varausesittely",
        TaskTypeNames.OpenHouse => "Yleisesittely",
        _ => "HuoneistoEsittely",
    };

    // -- Signature-pending source (SopimusVaraus — Tarjous) -------------------

    private const int OfferUrgentDays = 2;
    private const int OfferRowCap = 50;
    // TODO: 1000 days is a placeholder until we have a status-based filter on
    // SopimusVaraus (Sopimustila tunnukset). Without status filtering we keep
    // a wide lookback so legitimate awaiting-signature rows surface; once we
    // know which Sopimustila values mean "signature pending" we tighten this
    // back to ~30-90 days. Tracked in design/open-decisions.md (OD-A4-status).
    private const int OfferLookbackDays = 1000;

    private static IEnumerable<TaskDto> QuerySignaturePending(
        Session session, xVasuSecuritySystemUser user, DateOnly today, DateTime nowHelsinki)
    {
        var scope = UserAreaScope.Resolve(user);
        if (!scope.HasAccess)
        {
            return Array.Empty<TaskDto>();
        }

        var criteria = BuildOfferCriteria(scope, nowHelsinki);
        var collection = new XPCollection<XpoOffer>(session, criteria)
        {
            TopReturnedObjects = OfferRowCap,
        };
        collection.Sorting.Add(new SortProperty("Kirjattu", DevExpress.Xpo.DB.SortingDirection.Descending));
        return collection.Select(o => MapOfferCard(o, nowHelsinki)).ToList();
    }

    private static CriteriaOperator BuildOfferCriteria(UserAreaScope.Resolution scope, DateTime nowHelsinki)
    {
        // No status filter yet: SopimusTila is a lookup table whose tunnus
        // values aren't known to us. Cap the lookback window so the
        // backlog doesn't explode for users with active areas.
        var lookback = new BinaryOperator(
            "Kirjattu",
            nowHelsinki.AddDays(-OfferLookbackDays),
            BinaryOperatorType.GreaterOrEqual);

        if (scope.Unrestricted)
        {
            return lookback;
        }

        // Visibility: walk Talousyksikkö (Kustannuspaikka) → BranchCode and
        // match against the user's branchcodes (Kayttooikeudet). Same key
        // shape as Tiskilista.BranchCode.
        var visibility = new InOperator("Talousyksikkö.BranchCode", scope.Areas.Cast<object>().ToArray());
        return CriteriaOperator.And(visibility, lookback);
    }

    private static bool IsOfferVisibleTo(XpoOffer o, UserAreaScope.Resolution scope)
    {
        if (scope.Unrestricted) return true;
        if (scope.Areas.Count == 0) return false;
        var branch = o.Talousyksikkö?.BranchCode;
        return !string.IsNullOrEmpty(branch) && scope.Areas.Contains(branch, StringComparer.OrdinalIgnoreCase);
    }

    private static TaskDto MapOfferCard(XpoOffer o, DateTime nowHelsinki)
    {
        var (when, urgent) = FormatOfferWhen(o.Kirjattu, nowHelsinki);
        var title = FormatHuoneisto(o.Huoneisto)
            ?? (string.IsNullOrWhiteSpace(o.Header) ? "Tarjous" : o.Header);
        var who = ResolveOfferWho(o);
        var meta = ResolveOfferMeta(o);
        var idStr = o.VarausTunnus.ToString(CultureInfo.InvariantCulture);

        return new TaskDto(
            Id: idStr,
            Type: TaskTypeNames.SignaturePending,
            Accent: TaskAccents.Cta,
            Urgent: urgent,
            When: when,
            Title: title,
            Who: who,
            Meta: meta,
            TypeLabel: null,
            Actions:
            [
                new TaskActionDto(TaskActionKinds.Navigate, "Avaa tarjous", Primary: true, Destructive: false, Href: null),
            ],
            EntityRef: new TaskEntityRefDto("asma", "Tarjous", idStr),
            SortOrder: 0);
    }

    private static TaskDetailDto MapOfferDetail(XpoOffer o, DateTime nowHelsinki)
    {
        var (when, _) = FormatOfferWhen(o.Kirjattu, nowHelsinki);
        var title = FormatHuoneisto(o.Huoneisto)
            ?? (string.IsNullOrWhiteSpace(o.Header) ? "Tarjous" : o.Header);

        var paahakija = o.PaaHakijaAlias;
        var customer = paahakija is null
            ? null
            : new TaskCustomerDto(
                Id: paahakija.AsiakasNumero.ToString(CultureInfo.InvariantCulture),
                Name: paahakija.Kokonimi ?? paahakija.Header ?? "Asiakas",
                Initials: InitialsFor(paahakija.Kokonimi ?? paahakija.Header ?? string.Empty),
                Phone: string.IsNullOrWhiteSpace(paahakija.Gsm) ? null : paahakija.Gsm,
                Email: string.IsNullOrWhiteSpace(paahakija.Email) ? null : paahakija.Email);

        var subtitleParts = new List<string>();
        if (!string.IsNullOrWhiteSpace(o.Sopimustila?.SopimusTilaNimi))
        {
            subtitleParts.Add($"Tila {o.Sopimustila.SopimusTilaNimi}");
        }
        if (o.Vahvistus)
        {
            subtitleParts.Add("Hyväksytty asiakkaalla");
        }
        if (!string.IsNullOrWhiteSpace(o.EsittelijaNimi))
        {
            subtitleParts.Add($"Esittelijä {o.EsittelijaNimi}");
        }

        var timeContext = string.IsNullOrEmpty(when.Note)
            ? when.Time
            : $"{when.Time} · {when.Note}";

        var actions = new List<TaskRowActionDto>
        {
            new("navigate-offer", "Avaa tarjous",
                TaskRowActionKinds.Navigate, Destructive: false, Confirm: null, Href: null),
            new("navigate-unit", "Avaa kohde Lumo Verkossa",
                TaskRowActionKinds.External, Destructive: false, Confirm: null,
                Href: "https://www.lumo.fi/"),
            new("cancel", "Peruuta tarjous",
                TaskRowActionKinds.Cancel, Destructive: true,
                Confirm: new TaskConfirmDto("Peruuta",
                    "Peruutetaanko tarjous? Toiminto kirjataan auditiin."),
                Href: null),
        };

        var note = string.IsNullOrWhiteSpace(o.Muistio)
            ? null
            : new TaskNoteDto($"note-{o.VarausTunnus}", o.Muistio, DateTimeOffset.UtcNow);

        var idStr = o.VarausTunnus.ToString(CultureInfo.InvariantCulture);
        return new TaskDetailDto(
            Id: idStr,
            Type: TaskTypeNames.SignaturePending,
            TypeLabel: "Tarjous",
            Accent: TaskAccents.Cta,
            TimeContext: timeContext,
            Title: title,
            Subtitle: subtitleParts.Count == 0 ? null : string.Join(" · ", subtitleParts),
            Customer: customer,
            Actions: actions,
            Note: note,
            PrimaryCta: new TaskActionDto(
                TaskActionKinds.Navigate, "Avaa tarjous", Primary: true, Destructive: false, Href: null),
            EntityRef: new TaskEntityRefDto("asma", "Tarjous", idStr));
    }

    private static (TaskWhenDto when, bool urgent) FormatOfferWhen(DateTime kirjattu, DateTime nowHelsinki)
    {
        // Treat reservations as out-of-band ("Heti" / Today bucket). Days-since-
        // recorded surfaces in the secondary note; > N days flips the urgent flag.
        if (kirjattu == default || kirjattu.Year < 1900)
        {
            return (new TaskWhenDto("Heti", null, 0), false);
        }

        var days = Math.Max(0, (int)(nowHelsinki.Date - kirjattu.Date).TotalDays);
        var note = days == 0 ? "kirjattu tänään" : $"odottaa {days} pv";
        var urgent = days >= OfferUrgentDays;
        return (new TaskWhenDto("Heti", note, 0), urgent);
    }

    private static string? ResolveOfferWho(XpoOffer o)
    {
        var paahakija = o.PaaHakijaAlias;
        if (paahakija is null) return null;
        var name = paahakija.Kokonimi ?? paahakija.Header;
        if (string.IsNullOrWhiteSpace(name)) return null;
        var phone = paahakija.Gsm;
        return string.IsNullOrWhiteSpace(phone) ? name : $"{name} · {phone}";
    }

    private static string? ResolveOfferMeta(XpoOffer o)
    {
        var parts = new List<string>(2);
        if (!string.IsNullOrWhiteSpace(o.Sopimustila?.SopimusTilaNimi))
        {
            parts.Add(o.Sopimustila.SopimusTilaNimi);
        }
        if (o.SopimusAlkaa != default && o.SopimusAlkaa.Year > 1900)
        {
            parts.Add($"alkaa {o.SopimusAlkaa.Day}.{o.SopimusAlkaa.Month}.{o.SopimusAlkaa.Year}");
        }
        return parts.Count == 0 ? null : string.Join(" · ", parts);
    }

    // -- Shared formatting / bucketing -----------------------------------------

    private static TaskWhenDto FormatWhen(DateTime? due, DateOnly today)
    {
        if (due is null)
        {
            return new TaskWhenDto("Eräpäivä avoin", null, 0);
        }

        var dueDate = DateOnly.FromDateTime(due.Value);
        var hasTime = due.Value.TimeOfDay > TimeSpan.Zero;
        var sortOrder = ToUnixMs(due.Value);

        if (dueDate < today)
        {
            var overdueDays = today.DayNumber - dueDate.DayNumber;
            return new TaskWhenDto("Heti", overdueDays > 0 ? $"myöhässä {overdueDays} pv" : null, 0);
        }

        if (dueDate == today)
        {
            return hasTime
                ? new TaskWhenDto(due.Value.ToString("HH:mm", FiCulture), null, sortOrder)
                : new TaskWhenDto("Tänään", null, sortOrder);
        }

        if (dueDate == today.AddDays(1))
        {
            return hasTime
                ? new TaskWhenDto($"Huomenna klo {due.Value:HH\\:mm}", null, sortOrder)
                : new TaskWhenDto("Huomenna", null, sortOrder);
        }

        if (dueDate <= EndOfWeek(today))
        {
            var weekday = FiCulture.DateTimeFormat.GetAbbreviatedDayName(due.Value.DayOfWeek);
            return new TaskWhenDto(
                CapitaliseFirst(weekday),
                $"Valmis viimeistään {due.Value.Day}.{due.Value.Month}.",
                sortOrder);
        }

        return new TaskWhenDto($"{due.Value.Day}.{due.Value.Month}.", null, sortOrder);
    }

    private static IReadOnlyList<TaskGroupDto> BucketByDay(IList<TaskDto> dtos, DateOnly today)
    {
        var todays = new List<TaskDto>();
        var tomorrows = new List<TaskDto>();
        var weeks = new List<TaskDto>();
        var laters = new List<TaskDto>();

        var endOfWeek = EndOfWeek(today);
        var tomorrow = today.AddDays(1);

        foreach (var dto in dtos)
        {
            var date = DateFromUnix(dto.When.SortOrder);
            if (date is null || date.Value <= today) todays.Add(dto);
            else if (date.Value == tomorrow) tomorrows.Add(dto);
            else if (date.Value <= endOfWeek) weeks.Add(dto);
            else laters.Add(dto);
        }

        // Within each bucket: urgent rows first, then sortOrder, then accent priority.
        SortBucket(todays);
        SortBucket(tomorrows);
        SortBucket(weeks);
        SortBucket(laters);

        var groups = new List<TaskGroupDto>(4);
        if (todays.Count > 0) groups.Add(new TaskGroupDto(TaskGroupIds.Today, "Tänään", FormatDate(today), todays));
        if (tomorrows.Count > 0) groups.Add(new TaskGroupDto(TaskGroupIds.Tomorrow, "Huomenna", FormatDate(tomorrow), tomorrows));
        if (weeks.Count > 0) groups.Add(new TaskGroupDto(TaskGroupIds.ThisWeek, "Tällä viikolla", FormatDate(endOfWeek), weeks));
        if (laters.Count > 0) groups.Add(new TaskGroupDto(TaskGroupIds.Later, "Myöhemmin", FormatDate(today.AddDays(14)), laters));
        return groups;
    }

    private static void SortBucket(List<TaskDto> bucket) =>
        bucket.Sort((a, b) =>
        {
            var urgentCmp = b.Urgent.CompareTo(a.Urgent);
            if (urgentCmp != 0) return urgentCmp;
            var sortCmp = a.When.SortOrder.CompareTo(b.When.SortOrder);
            if (sortCmp != 0) return sortCmp;
            return AccentRank(a.Accent).CompareTo(AccentRank(b.Accent));
        });

    private static int AccentRank(string accent) => accent switch
    {
        TaskAccents.Cta => 0,
        TaskAccents.Warn => 1,
        TaskAccents.Info => 2,
        _ => 3, // Navy and unknown
    };

    private static DateOnly? DateFromUnix(long unixMs)
    {
        if (unixMs == 0) return null;
        var local = TimeZoneInfo.ConvertTimeFromUtc(
            DateTimeOffset.FromUnixTimeMilliseconds(unixMs).UtcDateTime,
            HelsinkiTz);
        return DateOnly.FromDateTime(local);
    }

    private static long ToUnixMs(DateTime localDt)
    {
        var unspecified = DateTime.SpecifyKind(localDt, DateTimeKind.Unspecified);
        var utc = TimeZoneInfo.ConvertTimeToUtc(unspecified, HelsinkiTz);
        return new DateTimeOffset(utc, TimeSpan.Zero).ToUnixTimeMilliseconds();
    }

    private static DateOnly EndOfWeek(DateOnly d)
    {
        var dow = (int)d.DayOfWeek;
        var offset = dow == 0 ? 0 : 7 - dow;        // Sunday → 0, Monday → 6, …
        return d.AddDays(offset);
    }

    private static string FormatDate(DateOnly d)
    {
        var dt = d.ToDateTime(TimeOnly.MinValue);
        var weekday = FiCulture.DateTimeFormat.GetAbbreviatedDayName(dt.DayOfWeek);
        return $"{weekday} {dt.Day}.{dt.Month}.";
    }

    private static string CapitaliseFirst(string s) =>
        string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s[1..];

    private static TasksResponseDto EmptyResponse() =>
        new(Array.Empty<TaskGroupDto>(), 0, DateTimeOffset.UtcNow);

    private static TimeZoneInfo ResolveHelsinkiTimeZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Europe/Helsinki"); }
        catch { return TimeZoneInfo.FindSystemTimeZoneById("FLE Standard Time"); }
    }
}
