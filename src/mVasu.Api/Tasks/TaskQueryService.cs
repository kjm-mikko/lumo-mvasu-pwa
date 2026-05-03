using System.Globalization;
using System.Security.Claims;
using mVasu.Api.Contracts;

namespace mVasu.Api.Tasks;

/// <summary>
/// Phase-1 mock implementation of <see cref="ITaskQueryService"/>. Returns
/// a hand-curated dataset that mirrors what the PWA used to ship in
/// <c>features/tasks/mock-tasks.ts</c>; once Phase 2 lands, the same
/// public surface will execute real XPO queries against the XAF entity
/// sources listed in BACKEND.md §2.
/// </summary>
public sealed class TaskQueryService : ITaskQueryService
{
    private static readonly TimeZoneInfo HelsinkiTz = ResolveHelsinkiTimeZone();
    private static readonly CultureInfo FiCulture = CultureInfo.GetCultureInfo("fi-FI");

    public Task<TaskDetailDto?> GetAsync(
        ClaimsPrincipal principal,
        string id,
        CancellationToken cancellationToken = default)
    {
        var nowHelsinki = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, HelsinkiTz);
        var allTasks = BuildMockGroups(nowHelsinki).SelectMany(g => g.Tasks);
        var card = allTasks.FirstOrDefault(t => t.Id == id);
        if (card is null)
        {
            return Task.FromResult<TaskDetailDto?>(null);
        }

        return Task.FromResult<TaskDetailDto?>(BuildDetail(card));
    }

    private static TaskDetailDto BuildDetail(TaskDto card)
    {
        var typeLabel = card.TypeLabel ?? TypeLabelFor(card.Type);

        var customer = card.Who is null ? null : BuildCustomer(card);

        var rowActions = BuildRowActions(card.Type, card.Id);

        var note = BuildNote(card);

        var primary = card.Actions.FirstOrDefault(a => a.Primary)
            ?? card.Actions.FirstOrDefault()
            ?? new TaskActionDto(TaskActionKinds.Navigate, "Avaa kohde", true, false, null);

        var timeContext = card.When.Note is null
            ? card.When.Time
            : $"{card.When.Time} · {card.When.Note}";

        return new TaskDetailDto(
            Id: card.Id,
            Type: card.Type,
            TypeLabel: typeLabel,
            Accent: card.Accent,
            TimeContext: timeContext,
            Title: card.Title,
            Subtitle: card.Meta,
            Customer: customer,
            Actions: rowActions,
            Note: note,
            PrimaryCta: primary,
            EntityRef: card.EntityRef);
    }

    private static TaskCustomerDto BuildCustomer(TaskDto card)
    {
        var who = card.Who ?? string.Empty;
        var nameOnly = who.Split('·')[0].Trim();
        var initials = InitialsFor(nameOnly);
        var phone = ExtractPhone(who);
        return new TaskCustomerDto(
            Id: $"customer-{card.Id}",
            Name: nameOnly,
            Initials: initials,
            Phone: phone,
            Email: null);
    }

    private static string InitialsFor(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "··";
        var parts = name.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0) return "··";
        if (parts.Length == 1) return parts[0][..Math.Min(2, parts[0].Length)].ToUpperInvariant();
        return $"{parts[0][0]}{parts[^1][0]}".ToUpperInvariant();
    }

    private static string? ExtractPhone(string who)
    {
        // Match a Finnish-style phone number embedded in the "who" string.
        var match = System.Text.RegularExpressions.Regex.Match(who, @"\+?\d[\d\s]{5,}");
        return match.Success ? match.Value.Trim() : null;
    }

    private static IReadOnlyList<TaskRowActionDto> BuildRowActions(string type, string taskId) => type switch
    {
        TaskTypeNames.VisitIntroduction or TaskTypeNames.VisitReservation =>
        [
            new TaskRowActionDto("navigate-unit", "Avaa kohde Lumo Verkossa",
                TaskRowActionKinds.External, Destructive: false, Confirm: null,
                Href: "https://www.lumo.fi/"),
            new TaskRowActionDto("create-offer", "Tee tarjous tästä",
                TaskRowActionKinds.CreateOffer, Destructive: false, Confirm: null, Href: null),
            new TaskRowActionDto("mark-done", "Merkitse pidetyksi",
                TaskRowActionKinds.MarkDone, Destructive: false,
                Confirm: new TaskConfirmDto("Käynti pidetty?", "Käynti merkitään pidetyksi."),
                Href: null),
            new TaskRowActionDto("cancel", "Peruuta",
                TaskRowActionKinds.Cancel, Destructive: true,
                Confirm: new TaskConfirmDto("Peruuta",
                    "Peruutetaanko tehtävä? Toiminto kirjataan auditiin."),
                Href: null),
        ],

        TaskTypeNames.SignaturePending =>
        [
            new TaskRowActionDto("navigate-unit", "Avaa kohde Lumo Verkossa",
                TaskRowActionKinds.External, Destructive: false, Confirm: null,
                Href: "https://www.lumo.fi/"),
            new TaskRowActionDto("mark-done", "Merkitse allekirjoitetuksi",
                TaskRowActionKinds.MarkDone, Destructive: false,
                Confirm: new TaskConfirmDto("Vahvistus", "Allekirjoitus merkitään valmiiksi."),
                Href: null),
            new TaskRowActionDto("cancel", "Peruuta",
                TaskRowActionKinds.Cancel, Destructive: true,
                Confirm: new TaskConfirmDto("Peruuta",
                    "Peruutetaanko tehtävä? Toiminto kirjataan auditiin."),
                Href: null),
        ],

        TaskTypeNames.InboxTermination or TaskTypeNames.InboxSigned =>
        [
            new TaskRowActionDto("mark-done", "Merkitse käsitellyksi",
                TaskRowActionKinds.MarkDone, Destructive: false,
                Confirm: new TaskConfirmDto("Vahvistus", "Tehtävä merkitään käsitellyksi."),
                Href: null),
        ],

        _ =>
        [
            new TaskRowActionDto("navigate-unit", "Avaa kohde Lumo Verkossa",
                TaskRowActionKinds.External, Destructive: false, Confirm: null,
                Href: "https://www.lumo.fi/"),
            new TaskRowActionDto("mark-done", "Merkitse tehdyksi",
                TaskRowActionKinds.MarkDone, Destructive: false,
                Confirm: new TaskConfirmDto("Vahvistus", "Tehtävä merkitään tehdyksi. Jatka?"),
                Href: null),
            new TaskRowActionDto("cancel", "Peruuta",
                TaskRowActionKinds.Cancel, Destructive: true,
                Confirm: new TaskConfirmDto("Peruuta",
                    "Peruutetaanko tehtävä? Toiminto kirjataan auditiin."),
                Href: null),
        ],
    };

    private static TaskNoteDto BuildNote(TaskDto card)
    {
        var body = card.Type switch
        {
            TaskTypeNames.VisitIntroduction or TaskTypeNames.VisitReservation
                => "Asiakas on muuttamassa kohti pääkaupunkiseutua. Painottaa rauhallista naapurustoa.",
            TaskTypeNames.InboxTermination
                => "Asiakas vahvisti irtisanomisen sähköpostissa 30.4. Ei lisätietoja.",
            _ => string.Empty,
        };
        return new TaskNoteDto(
            Id: $"note-{card.Id}",
            Body: body,
            UpdatedAt: DateTimeOffset.UtcNow);
    }

    private static string TypeLabelFor(string type) => type switch
    {
        TaskTypeNames.VisitIntroduction  => "Tutustumiskäynti",
        TaskTypeNames.VisitReservation   => "Varausesittely",
        TaskTypeNames.OpenHouse          => "Yleisesittely",
        TaskTypeNames.SignaturePending   => "Allekirjoitus",
        TaskTypeNames.InboxTermination   => "Saapunut irtisanominen",
        TaskTypeNames.InboxSigned        => "Allekirjoitettu palautunut",
        TaskTypeNames.PhotoScheduled     => "Valokuvaus",
        TaskTypeNames.RenovationApproval => "Remontti",
        TaskTypeNames.LeadCallback       => "Liidi",
        TaskTypeNames.DeskListItem       => "Tiskilista",
        _                                => "Tehtävä",
    };

    public Task<TasksResponseDto> ListAsync(
        ClaimsPrincipal principal,
        TaskQueryParameters query,
        CancellationToken cancellationToken = default)
    {
        var nowHelsinki = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, HelsinkiTz);
        var groups = BuildMockGroups(nowHelsinki);

        var filtered = groups
            .Select(g => g with
            {
                Tasks = g.Tasks
                    .Where(t => query.Types is null || query.Types.Count == 0 || query.Types.Contains(t.Type))
                    .Where(t => !query.UrgentOnly || t.Urgent)
                    .ToList()
            })
            .Where(g => g.Tasks.Count > 0)
            .ToList();

        var total = filtered.Sum(g => g.Tasks.Count);
        return Task.FromResult(new TasksResponseDto(filtered, total, DateTimeOffset.UtcNow));
    }

    private static List<TaskGroupDto> BuildMockGroups(DateTime nowHelsinki)
    {
        var today = DateOnly.FromDateTime(nowHelsinki);
        return
        [
            new TaskGroupDto(
                TaskGroupIds.Today,
                "Tänään",
                FormatDate(today),
                Today()),
            new TaskGroupDto(
                TaskGroupIds.Tomorrow,
                "Huomenna",
                FormatDate(today.AddDays(1)),
                Tomorrow()),
            new TaskGroupDto(
                TaskGroupIds.ThisWeek,
                "Tällä viikolla",
                FormatDate(EndOfWeek(today)),
                ThisWeek()),
            new TaskGroupDto(
                TaskGroupIds.Later,
                "Myöhemmin",
                FormatDate(today.AddDays(14)),
                Later()),
        ];
    }

    private static List<TaskDto> Today() =>
    [
        new(
            "mock-today-1",
            TaskTypeNames.SignaturePending,
            TaskAccents.Cta,
            Urgent: true,
            new TaskWhenDto("Heti", "odottaa 3 pv", 0),
            "Allekirjoitusta odottaa",
            "Asiakas_001",
            "Vänrikinkatu 2 · 2H+K · 48,5 m²",
            TypeLabel: null,
            [
                new TaskActionDto(TaskActionKinds.Navigate, "Avaa tarjous", Primary: true,  Destructive: false, "/contracts/mock-1"),
                new TaskActionDto(TaskActionKinds.Phone,    "Soita",         Primary: false, Destructive: false, "tel:+358000000"),
            ],
            new TaskEntityRefDto("asma", "Tarjous", "mock-1"),
            SortOrder: 0),

        new(
            "mock-today-2",
            TaskTypeNames.VisitIntroduction,
            TaskAccents.Navy,
            Urgent: false,
            new TaskWhenDto("10:30", "Kesto 30 min", UnixMs("10:30")),
            "Mannerheimintie 12 A 4",
            "Asiakas_002 · 044 PLACEHOLDER",
            "Esittelijä Esittelijä_A",
            TypeLabel: null,
            [
                new TaskActionDto(TaskActionKinds.Navigate, "Avaa kohde", Primary: true,  Destructive: false, null),
                new TaskActionDto(TaskActionKinds.Phone,    "Soita",      Primary: false, Destructive: false, "tel:+358000001"),
                new TaskActionDto(TaskActionKinds.Sms,      "Viesti",     Primary: false, Destructive: false, "sms:+358000001"),
            ],
            new TaskEntityRefDto("core", "Tutustumiskaynti", "mock-2"),
            SortOrder: 1),

        new(
            "mock-today-3",
            TaskTypeNames.OpenHouse,
            TaskAccents.Navy,
            Urgent: false,
            new TaskWhenDto("14:00", "Kesto 15 min", UnixMs("14:00")),
            "Maauunintie 23 A 2, 01450 Vantaa",
            null,
            "4 ilmoittautunutta · Tervetuloa esittelyyn, A-rapun edessä",
            TypeLabel: null,
            [
                new TaskActionDto(TaskActionKinds.Navigate, "Avaa esittely", Primary: true, Destructive: false, null),
            ],
            new TaskEntityRefDto("core", "Yleisesittely", "mock-3"),
            SortOrder: 2),

        new(
            "mock-today-4",
            TaskTypeNames.InboxTermination,
            TaskAccents.Info,
            Urgent: false,
            new TaskWhenDto("Tänään", "käsittele", 0),
            "Saapunut irtisanominen",
            "Asiakas_003",
            "Sopimus #SOP-PLACEHOLDER · Vänrikinkatu 2",
            TypeLabel: null,
            [
                new TaskActionDto(TaskActionKinds.Navigate, "Käsittele", Primary: true, Destructive: false, null),
            ],
            new TaskEntityRefDto("asma", "Irtisanomisilmoitus", "mock-4"),
            SortOrder: 3),
    ];

    private static List<TaskDto> Tomorrow() =>
    [
        new(
            "mock-tomorrow-1",
            TaskTypeNames.VisitReservation,
            TaskAccents.Navy,
            Urgent: false,
            new TaskWhenDto("09:00", "Kesto 30 min", UnixMs("09:00", days: 1)),
            "Asemakuja 1 B 69, 02770 Espoo",
            "Päähakija Asiakas_004",
            "Varattu · Sopimustila Toistaiseksi",
            TypeLabel: null,
            [
                new TaskActionDto(TaskActionKinds.Navigate, "Avaa varausesittely", Primary: true, Destructive: false, null),
            ],
            new TaskEntityRefDto("core", "Varausesittely", "mock-5"),
            SortOrder: 0),

        new(
            "mock-tomorrow-2",
            TaskTypeNames.PhotoScheduled,
            TaskAccents.Navy,
            Urgent: false,
            new TaskWhenDto("13:00", "Kuvauspalvelu", UnixMs("13:00", days: 1)),
            "Hämeenkatu 7, 33100 Tampere",
            null,
            "Kuvaaja paikalla 1 h · tilaaja Esittelijä_B",
            TypeLabel: null,
            [
                new TaskActionDto(TaskActionKinds.Navigate, "Avaa kohde", Primary: true,  Destructive: false, null),
                new TaskActionDto(TaskActionKinds.Cancel,   "Peruuta",    Primary: false, Destructive: true,  null),
            ],
            new TaskEntityRefDto("asma", "Valokuvaus", "mock-6"),
            SortOrder: 1),

        new(
            "mock-tomorrow-3",
            TaskTypeNames.LeadCallback,
            TaskAccents.Navy,
            Urgent: false,
            new TaskWhenDto("15:00", "soittopyyntö", UnixMs("15:00", days: 1)),
            "Liidi: kiinnostunut kahdesta kohteesta",
            "Asiakas_005",
            "Viimeinen kontakti 3 pv sitten",
            TypeLabel: null,
            [
                new TaskActionDto(TaskActionKinds.Phone,    "Soita",                Primary: true,  Destructive: false, "tel:+358000002"),
                new TaskActionDto(TaskActionKinds.MarkDone, "Merkitse tehdyksi",    Primary: false, Destructive: false, null),
            ],
            new TaskEntityRefDto("asma", "Liidi", "mock-7"),
            SortOrder: 2),
    ];

    private static List<TaskDto> ThisWeek() =>
    [
        new(
            "mock-week-tehtava-1",
            TaskTypeNames.GenericTask,
            TaskAccents.Warn,
            Urgent: false,
            new TaskWhenDto("Ke", "Valmis viimeistään 7.5.", UnixMs("09:00", days: 3)),
            "Hinnoittelun tarkistus uusilla kohteilla",
            "Huoneisto: Vänrikinkatu 2 A 4, 00150 Helsinki",
            "Tehtävätyyppi Hinnoittelu · Priority High · Status NotStarted",
            TypeLabel: "Hinnoittelu",
            [
                new TaskActionDto(TaskActionKinds.Navigate, "Avaa tehtävä",       Primary: true,  Destructive: false, null),
                new TaskActionDto(TaskActionKinds.MarkDone, "Merkitse tehdyksi",  Primary: false, Destructive: false, null),
            ],
            new TaskEntityRefDto("core", "xVasuSecuritySystemUserTask", "mock-tehtava-1"),
            SortOrder: 0),

        new(
            "mock-week-tehtava-2",
            TaskTypeNames.GenericTask,
            TaskAccents.Navy,
            Urgent: false,
            new TaskWhenDto("To", "Valmis viimeistään 8.5.", UnixMs("09:00", days: 4)),
            "Muuttotarkastus sopimuksen päättyessä",
            "Sopimus #SOP-PLACEHOLDER · Tilakoodi \"Päättyy\"",
            "Tehtävätyyppi Tarkastus · Priority Normal · Status InProgress · 40 % valmiina",
            TypeLabel: "Tarkastus",
            [
                new TaskActionDto(TaskActionKinds.Navigate, "Avaa tehtävä", Primary: true, Destructive: false, null),
            ],
            new TaskEntityRefDto("core", "xVasuSecuritySystemUserTask", "mock-tehtava-2"),
            SortOrder: 1),

        new(
            "mock-week-1",
            TaskTypeNames.RenovationApproval,
            TaskAccents.Warn,
            Urgent: false,
            new TaskWhenDto("Pe", "tarkista", UnixMs("09:00", days: 5)),
            "Remonttihyväksyntää odottaa",
            null,
            "Kohde B07 · keittiön pintaremontti",
            TypeLabel: null,
            [
                new TaskActionDto(TaskActionKinds.Navigate, "Avaa remontti", Primary: true, Destructive: false, null),
            ],
            new TaskEntityRefDto("kire", "Remontti", "mock-8"),
            SortOrder: 2),

        new(
            "mock-week-2",
            TaskTypeNames.InboxSigned,
            TaskAccents.Info,
            Urgent: false,
            new TaskWhenDto("To", null, UnixMs("12:00", days: 4)),
            "Allekirjoitettu sopimus arkistoitavaksi",
            "Asiakas_006",
            "Sähköinen allekirjoitus palautunut",
            TypeLabel: null,
            [
                new TaskActionDto(TaskActionKinds.MarkDone, "Merkitse käsitellyksi", Primary: true, Destructive: false, null),
            ],
            new TaskEntityRefDto("asma", "Allekirjoitustapahtuma", "mock-9"),
            SortOrder: 3),
    ];

    private static List<TaskDto> Later() =>
    [
        new(
            "mock-later-1",
            TaskTypeNames.DeskListItem,
            TaskAccents.Navy,
            Urgent: false,
            new TaskWhenDto("12.5.", "vapautuu", UnixMs("12:00", days: 14)),
            "Tiskilistalle siirtyvä huoneisto",
            null,
            "Kohde C03 · 3H+K+S",
            TypeLabel: null,
            [
                new TaskActionDto(TaskActionKinds.Navigate, "Avaa kohde", Primary: true, Destructive: false, null),
            ],
            new TaskEntityRefDto("kire", "Tiskilista", "mock-10"),
            SortOrder: 0),
    ];

    private static long UnixMs(string hhmm, int days = 0)
    {
        var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, HelsinkiTz);
        var target = new DateTime(now.Year, now.Month, now.Day, 0, 0, 0, DateTimeKind.Unspecified)
            .AddDays(days);
        if (TimeOnly.TryParse(hhmm, FiCulture, DateTimeStyles.None, out var parsed))
        {
            target = target.AddHours(parsed.Hour).AddMinutes(parsed.Minute);
        }
        var utc = TimeZoneInfo.ConvertTimeToUtc(target, HelsinkiTz);
        return new DateTimeOffset(utc, TimeSpan.Zero).ToUnixTimeMilliseconds();
    }

    private static DateOnly EndOfWeek(DateOnly d)
    {
        var dow = (int)d.DayOfWeek;
        var offset = dow == 0 ? 0 : 7 - dow;        // sunday → 0, monday → 6, …
        return d.AddDays(offset);
    }

    private static string FormatDate(DateOnly d)
    {
        // Short Finnish weekday + day.month — matches frontend's existing
        // FI_DATE format (e.g. "la 4.5.").
        var dt = d.ToDateTime(TimeOnly.MinValue);
        return $"{FiCulture.DateTimeFormat.GetAbbreviatedDayName(dt.DayOfWeek)} {dt.Day}.{dt.Month}.";
    }

    private static TimeZoneInfo ResolveHelsinkiTimeZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Europe/Helsinki"); }
        catch { return TimeZoneInfo.FindSystemTimeZoneById("FLE Standard Time"); }
    }
}
