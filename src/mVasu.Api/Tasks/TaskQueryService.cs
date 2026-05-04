using System.Globalization;
using System.Security.Claims;
using DevExpress.Data.Filtering;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Xpo;
using DevExpress.Xpo;
using mVasu.Api.Authentication;
using mVasu.Api.Contracts;
using xVasu.Data.Security;
using XpoTask = xVasu.Data.Security.xVasuSecuritySystemUserTask;
using XpoPriority = xVasu.Data.Security.Priority;
using XpoTaskStatus = xVasu.Data.Security.TaskStatus;

namespace mVasu.Api.Tasks;

/// <summary>
/// Reads <c>xVasuSecuritySystemUserTask</c> rows visible to the authenticated
/// user. Visibility mirrors NAVIGATION.md §3a — owner OR assigned-to OR
/// additional-user. Other domain task sources (Tutustumiskaynti, Yleisesittely,
/// Varausesittely, Tarjous, Saapuneet irtisanomiset, Valokuvaus, Remontti,
/// Liidi, Tiskilista) land in subsequent A2-A8 phases; this service emits
/// only <see cref="TaskTypeNames.GenericTask"/> rows for now.
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
        // If the caller filtered to only non-generic types we don't yet cover, return empty.
        if (query.Types is { Count: > 0 } && !query.Types.Contains(TaskTypeNames.GenericTask))
        {
            return Task.FromResult(EmptyResponse());
        }

        var (user, os) = ResolveUser(principal);
        if (user is null || os is null)
        {
            return Task.FromResult(EmptyResponse());
        }

        try
        {
            var nowHelsinki = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, HelsinkiTz);
            var today = DateOnly.FromDateTime(nowHelsinki);
            var fromDate = query.From ?? today;
            var toDate = query.To ?? today.AddDays(30);

            var criteria = BuildCriteria(user.Oid, fromDate, toDate, query.UrgentOnly);
            var session = ((XPObjectSpace)os).Session;
            var collection = new XPCollection<XpoTask>(session, criteria);
            collection.Sorting.Add(new SortProperty("DueDate", DevExpress.Xpo.DB.SortingDirection.Ascending));

            var dtos = collection.Select(t => MapCard(t, today)).ToList();
            var groups = BucketByDay(dtos, today);
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
        if (!Guid.TryParse(id, out var oid))
        {
            return Task.FromResult<TaskDetailDto?>(null);
        }

        var (user, os) = ResolveUser(principal);
        if (user is null || os is null)
        {
            return Task.FromResult<TaskDetailDto?>(null);
        }

        try
        {
            var task = os.GetObjectByKey<XpoTask>(oid);
            if (task is null)
            {
                return Task.FromResult<TaskDetailDto?>(null);
            }

            if (!IsVisibleTo(task, user.Oid))
            {
                logger.LogInformation("Task {Oid} access denied for user {UserOid}", oid, user.Oid);
                return Task.FromResult<TaskDetailDto?>(null);
            }

            var nowHelsinki = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, HelsinkiTz);
            var today = DateOnly.FromDateTime(nowHelsinki);
            return Task.FromResult<TaskDetailDto?>(MapDetail(task, today));
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

    private static CriteriaOperator BuildCriteria(Guid userOid, DateOnly from, DateOnly to, bool urgentOnly)
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
        };

        var fromDt = from.ToDateTime(TimeOnly.MinValue);
        var toDt = to.ToDateTime(new TimeOnly(23, 59, 59));

        // Include rows with no DueDate (default DateTime in legacy data) so they
        // surface as "Today" rather than disappearing.
        operands.Add(CriteriaOperator.Or(
            new BinaryOperator("DueDate", new DateTime(1900, 1, 1), BinaryOperatorType.Less),
            CriteriaOperator.And(
                new BinaryOperator("DueDate", fromDt, BinaryOperatorType.GreaterOrEqual),
                new BinaryOperator("DueDate", toDt, BinaryOperatorType.LessOrEqual))));

        if (urgentOnly)
        {
            operands.Add(new BinaryOperator("Priority", XpoPriority.High, BinaryOperatorType.Equal));
        }

        return CriteriaOperator.And(operands);
    }

    private static bool IsVisibleTo(XpoTask t, Guid userOid)
    {
        if (t.Owner?.Oid == userOid) return true;
        if (t.AssignedTo?.Oid == userOid) return true;
        return t.xVasuUserTaskAdditionalUsers.OfType<xVasuUserTaskAdditionalUser>()
            .Any(au => au.UserID?.Oid == userOid);
    }

    private static TaskDto MapCard(XpoTask t, DateOnly today)
    {
        var due = ExtractDueDate(t);
        var when = FormatWhen(due, today);
        var (accent, urgent) = DeriveAccent(t.Priority, due, today);
        var typeLabel = t.UserTaskType?.Name;
        var who = ResolveWho(t);
        var meta = ResolveMeta(t);

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

    private static TaskDetailDto MapDetail(XpoTask t, DateOnly today)
    {
        var due = ExtractDueDate(t);
        var when = FormatWhen(due, today);
        var (accent, _) = DeriveAccent(t.Priority, due, today);
        var typeLabel = t.UserTaskType?.Name ?? "Tehtävä";
        var meta = ResolveMeta(t);

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
        // Legacy default value (XAF stores DateTime.MinValue or near-min for "unset")
        if (due < new DateTime(1900, 1, 1)) return null;
        return due;
    }

    private static string? ResolveWho(XpoTask t)
    {
        // Show the *other* party so the row tells the user what they need to know
        // — assigned-to from the owner's perspective, owner from the assignee's.
        var assignee = t.AssignedTo?.Kokonimi;
        var owner = t.Owner?.Kokonimi;
        if (!string.IsNullOrWhiteSpace(assignee)) return assignee;
        if (!string.IsNullOrWhiteSpace(owner)) return owner;
        return null;
    }

    private static string? ResolveMeta(XpoTask t)
    {
        // Prefer the most specific anchor: Huoneisto > Sopimus > Kohde.
        if (t.Huoneisto is not null)
        {
            // Huoneisto.ToString() returns the address-shaped header; reflection
            // confirms the Huoneisto type lives in xVasu.Data.Kire.
            var header = t.Huoneisto.ToString();
            if (!string.IsNullOrWhiteSpace(header)) return header;
        }
        if (t.Sopimus is not null)
        {
            var s = t.Sopimus.ToString();
            if (!string.IsNullOrWhiteSpace(s)) return s;
        }
        if (t.Kohde is not null)
        {
            // Kustannuspaikka.Header is a PersistentAlias that composes
            // "{Kptunnus} - {KpNimi} {Kunta} {Osoite}".
            var h = t.Kohde.Header;
            if (!string.IsNullOrWhiteSpace(h)) return h;
        }
        return null;
    }

    private static (string accent, bool urgent) DeriveAccent(XpoPriority priority, DateTime? due, DateOnly today)
    {
        var overdue = due is { } d && DateOnly.FromDateTime(d) < today;
        if (priority == XpoPriority.High || overdue) return (TaskAccents.Warn, true);
        if (priority == XpoPriority.Low) return (TaskAccents.Info, false);
        return (TaskAccents.Navy, false);
    }

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
            if (date is null || date.Value <= today)
            {
                todays.Add(dto);
            }
            else if (date.Value == tomorrow)
            {
                tomorrows.Add(dto);
            }
            else if (date.Value <= endOfWeek)
            {
                weeks.Add(dto);
            }
            else
            {
                laters.Add(dto);
            }
        }

        var groups = new List<TaskGroupDto>(4);
        if (todays.Count > 0) groups.Add(new TaskGroupDto(TaskGroupIds.Today, "Tänään", FormatDate(today), todays));
        if (tomorrows.Count > 0) groups.Add(new TaskGroupDto(TaskGroupIds.Tomorrow, "Huomenna", FormatDate(tomorrow), tomorrows));
        if (weeks.Count > 0) groups.Add(new TaskGroupDto(TaskGroupIds.ThisWeek, "Tällä viikolla", FormatDate(endOfWeek), weeks));
        if (laters.Count > 0) groups.Add(new TaskGroupDto(TaskGroupIds.Later, "Myöhemmin", FormatDate(today.AddDays(14)), laters));
        return groups;
    }

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
        // Treat XPO DateTime as Helsinki-local since XAF persists naive timestamps.
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
