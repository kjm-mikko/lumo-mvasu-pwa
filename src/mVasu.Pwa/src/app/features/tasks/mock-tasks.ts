import type { TaskCard, TaskGroup, TaskGroupId } from '../../core/models/task.dto';
import type { TaskDetail, TaskRowAction } from '../../core/models/task-detail.dto';

/**
 * Static mock data for the Task tab while the backend is being designed.
 *
 * Canonical Tehtava entity: `xVasu.Data.Security.xVasuSecuritySystemUserTask`
 * (NAVIGATION.md §3, ListView `xVasuSecuritySystemUser_…TasksFull_ListView`).
 * Real fields: Subject, UserTaskType, Priority, Status, StartDate, DueDate,
 * ReminderTime, DateCompleted, PercentCompleted, Description, Owner,
 * AssignedTo, Kohde, Huoneisto, Sopimus, Huoneistot, Sopimukset,
 * Kustannuspaikat, xVasuUserTaskAdditionalUsers, IsPrivate.
 *
 * Per-card source comments below cite the XAF entity each row would come
 * from once the BACKEND.md aggregator exists (EXISTING_ENTITIES.md fields):
 * - visit-introduction → Tutustumiskäynti (Osoite, Asiakas, Gsm, Alkuaika,
 *   Kesto, Esittelijä, SopimuksenPeruutusViimeistään)
 * - open-house         → Yleisesittely (Osoite, RuokakuntiaYleisesittelyssä,
 *   LyhytSelite, Esittelyaika, Esittelynkesto, Sopimustila)
 * - visit-reservation  → Varausesittely (Osoite, Päähakija, Esittelyaika,
 *   Esittelynkesto, Sopimustila)
 * - signature-pending  → Tarjous / Sopimus (Status, AllekirjoitusPaiva,
 *   LuontiPaiva, Vastuuhenkilo)
 * - inbox-termination  → Irtisanomisilmoitus (Tila, KasittelyTila,
 *   VastuuHenkilo)
 * - photo-scheduled    → Valokuvaus (Status, Aikataulu, TilaajaHenkilo)
 * - renovation-approval→ Remontti (Tila, HyvaksyjaHenkilo)
 * - lead-callback      → Liidi (Käsittelytila, Soittoaika, Hakukunnat,
 *   Maksimivuokra, Tyyppi)
 * - desk-list-item     → Tiskilista (Status)
 * - generic            → xVasuSecuritySystemUserTask (Subject, UserTaskType,
 *   Priority, Status, DueDate, Huoneisto, Sopimus)
 *
 * No real customer data — names are synthetic placeholders only
 * (Asiakas_001 etc.) per privacy policy.
 */

const today: ReadonlyArray<TaskCard> = [
  {
    // Source: Tarjous (Status='hyvaksytty_asiakkaalla', AllekirjoitusPaiva=null)
    id: 'mock-today-1',
    type: 'signature-pending',
    accent: 'cta',
    urgent: true,
    when: { time: 'Heti', note: 'odottaa 3 pv' },
    title: 'Allekirjoitusta odottaa',
    who: 'Asiakas_001',
    meta: 'Vänrikinkatu 2 · 2H+K · 48,5 m²',
    actions: [
      { kind: 'navigate', label: 'Avaa tarjous', primary: true, href: '/contracts/mock-1' },
      { kind: 'phone',    label: 'Soita',       href: 'tel:+358000000' },
    ],
    entityRef: { module: 'asma', id: 'mock-1' },
    sortOrder: 0,
  },
  {
    // Source: Tutustumiskäynti (Alkuaika, Esittelijä, SopimuksenPeruutusViimeistään)
    id: 'mock-today-2',
    type: 'visit-introduction',
    accent: 'navy',
    urgent: false,
    when: { time: '10:30', note: 'Kesto 30 min' },
    title: 'Mannerheimintie 12 A 4',
    who: 'Asiakas_002 · 044 PLACEHOLDER',
    meta: 'Esittelijä Esittelijä_A',
    actions: [
      { kind: 'navigate', label: 'Avaa kohde', primary: true },
      { kind: 'phone',    label: 'Soita', href: 'tel:+358000001' },
      { kind: 'sms',      label: 'Viesti', href: 'sms:+358000001' },
    ],
    entityRef: { module: 'core', id: 'mock-2' },
    sortOrder: 1,
  },
  {
    // Source: Yleisesittely (RuokakuntiaYleisesittelyssä, LyhytSelite, Esittelyaika)
    id: 'mock-today-3',
    type: 'open-house',
    accent: 'navy',
    urgent: false,
    when: { time: '14:00', note: 'Kesto 15 min' },
    title: 'Maauunintie 23 A 2, 01450 Vantaa',
    meta: '4 ilmoittautunutta · Tervetuloa esittelyyn, A-rapun edessä',
    actions: [
      { kind: 'navigate', label: 'Avaa esittely', primary: true },
    ],
    entityRef: { module: 'core', id: 'mock-3' },
    sortOrder: 2,
  },
  {
    // Source: Irtisanomisilmoitus (Tila='saapunut', KasittelyTila='avaamatta')
    id: 'mock-today-4',
    type: 'inbox-termination',
    accent: 'info',
    urgent: false,
    when: { time: 'Tänään', note: 'käsittele' },
    title: 'Saapunut irtisanominen',
    who: 'Asiakas_003',
    meta: 'Sopimus #SOP-PLACEHOLDER · Vänrikinkatu 2',
    actions: [
      { kind: 'navigate', label: 'Käsittele', primary: true },
    ],
    entityRef: { module: 'asma', id: 'mock-4' },
    sortOrder: 3,
  },
];

const tomorrow: ReadonlyArray<TaskCard> = [
  {
    // Source: Varausesittely (Päähakija, Esittelyaika, Sopimustila='Toistaiseksi')
    id: 'mock-tomorrow-1',
    type: 'visit-reservation',
    accent: 'navy',
    urgent: false,
    when: { time: '09:00', note: 'Kesto 30 min' },
    title: 'Asemakuja 1 B 69, 02770 Espoo',
    who: 'Päähakija Asiakas_004',
    meta: 'Varattu · Sopimustila Toistaiseksi',
    actions: [
      { kind: 'navigate', label: 'Avaa varausesittely', primary: true },
    ],
    entityRef: { module: 'core', id: 'mock-5' },
    sortOrder: 0,
  },
  {
    // Source: Valokuvaus (Status='tilattu', Aikataulu, TilaajaHenkilo)
    id: 'mock-tomorrow-2',
    type: 'photo-scheduled',
    accent: 'navy',
    urgent: false,
    when: { time: '13:00', note: 'Kuvauspalvelu' },
    title: 'Hämeenkatu 7, 33100 Tampere',
    meta: 'Kuvaaja paikalla 1 h · tilaaja Esittelijä_B',
    actions: [
      { kind: 'navigate', label: 'Avaa kohde', primary: true },
      { kind: 'cancel',   label: 'Peruuta', destructive: true },
    ],
    entityRef: { module: 'asma', id: 'mock-6' },
    sortOrder: 1,
  },
  {
    // Source: Liidi (Status='avoin', ViimeinenKontakti < now-2d)
    id: 'mock-tomorrow-3',
    type: 'lead-callback',
    accent: 'navy',
    urgent: false,
    when: { time: '15:00', note: 'soittopyyntö' },
    title: 'Liidi: kiinnostunut kahdesta kohteesta',
    who: 'Asiakas_005',
    meta: 'Viimeinen kontakti 3 pv sitten',
    actions: [
      { kind: 'phone', label: 'Soita', primary: true, href: 'tel:+358000002' },
      { kind: 'mark-done', label: 'Merkitse tehdyksi' },
    ],
    entityRef: { module: 'asma', id: 'mock-7' },
    sortOrder: 2,
  },
];

const thisWeek: ReadonlyArray<TaskCard> = [
  {
    // Source: xVasuSecuritySystemUserTask (Subject, UserTaskType.Name='Hinnoittelu',
    //   Priority=High, Status=NotStarted, DueDate, Huoneisto.Osoite)
    id: 'mock-week-tehtava-1',
    type: 'lead-callback', // closest existing accent bucket; UserTaskType label drives row-1 caption
    typeLabel: 'Hinnoittelu',
    accent: 'warn',
    urgent: false,
    when: { time: 'Ke', note: 'Valmis viimeistään 7.5.' },
    title: 'Hinnoittelun tarkistus uusilla kohteilla',
    who: 'Huoneisto: Vänrikinkatu 2 A 4, 00150 Helsinki',
    meta: 'Tehtävätyyppi Hinnoittelu · Priority High · Status NotStarted',
    actions: [
      { kind: 'navigate',  label: 'Avaa tehtävä', primary: true },
      { kind: 'mark-done', label: 'Merkitse tehdyksi' },
    ],
    entityRef: { module: 'core', id: 'mock-tehtava-1' },
    sortOrder: 0,
  },
  {
    // Source: xVasuSecuritySystemUserTask (Subject, UserTaskType.Name='Tarkastus',
    //   Priority=Normal, Status=InProgress, DueDate, Sopimus.Tilakoodi.TilaKoodiNimi)
    id: 'mock-week-tehtava-2',
    type: 'lead-callback',
    typeLabel: 'Tarkastus',
    accent: 'navy',
    urgent: false,
    when: { time: 'To', note: 'Valmis viimeistään 8.5.' },
    title: 'Muuttotarkastus sopimuksen päättyessä',
    who: 'Sopimus #SOP-PLACEHOLDER · Tilakoodi “Päättyy”',
    meta: 'Tehtävätyyppi Tarkastus · Priority Normal · Status InProgress · 40 % valmiina',
    actions: [
      { kind: 'navigate', label: 'Avaa tehtävä', primary: true },
    ],
    entityRef: { module: 'core', id: 'mock-tehtava-2' },
    sortOrder: 1,
  },
  {
    // Source: Remontti (Tila='odottaa_hyvaksyntaa', HyvaksyjaHenkilo)
    id: 'mock-week-1',
    type: 'renovation-approval',
    accent: 'warn',
    urgent: false,
    when: { time: 'Pe', note: 'tarkista' },
    title: 'Remonttihyväksyntää odottaa',
    meta: 'Kohde B07 · keittiön pintaremontti',
    actions: [
      { kind: 'navigate', label: 'Avaa remontti', primary: true },
    ],
    entityRef: { module: 'kire', id: 'mock-8' },
    sortOrder: 2,
  },
  {
    // Source: Tehtävä (Otsikko, Tehtävätyyppi, Priority='Medium', Status='Not started')
    id: 'mock-week-2',
    type: 'inbox-signed',
    accent: 'info',
    urgent: false,
    when: { time: 'To' },
    title: 'Allekirjoitettu sopimus arkistoitavaksi',
    who: 'Asiakas_006',
    meta: 'Sähköinen allekirjoitus palautunut',
    actions: [
      { kind: 'mark-done', label: 'Merkitse käsitellyksi', primary: true },
    ],
    entityRef: { module: 'asma', id: 'mock-9' },
    sortOrder: 3,
  },
];

const later: ReadonlyArray<TaskCard> = [
  {
    // Source: Tiskilista (Status='avoin')
    id: 'mock-later-1',
    type: 'desk-list-item',
    accent: 'navy',
    urgent: false,
    when: { time: '12.5.', note: 'vapautuu' },
    title: 'Tiskilistalle siirtyvä huoneisto',
    meta: 'Kohde C03 · 3H+K+S',
    actions: [
      { kind: 'navigate', label: 'Avaa kohde', primary: true },
    ],
    entityRef: { module: 'kire', id: 'mock-10' },
    sortOrder: 0,
  },
];

interface MockGroupSeed {
  readonly id: TaskGroupId;
  readonly label: string;
  readonly tasks: ReadonlyArray<TaskCard>;
}

const seeds: ReadonlyArray<MockGroupSeed> = [
  { id: 'today',     label: 'Tänään',          tasks: today },
  { id: 'tomorrow',  label: 'Huomenna',        tasks: tomorrow },
  { id: 'this-week', label: 'Tällä viikolla',  tasks: thisWeek },
  { id: 'later',     label: 'Myöhemmin',       tasks: later },
];

const FI_DATE = new Intl.DateTimeFormat('fi-FI', {
  weekday: 'short',
  day: 'numeric',
  month: 'numeric',
});

function dateForGroup(id: TaskGroupId, now: Date): string {
  const d = new Date(now);
  d.setHours(0, 0, 0, 0);
  switch (id) {
    case 'today':
      break;
    case 'tomorrow':
      d.setDate(d.getDate() + 1);
      break;
    case 'this-week': {
      const day = d.getDay();
      const offset = day === 0 ? 0 : 7 - day;
      d.setDate(d.getDate() + offset);
      break;
    }
    case 'later':
      d.setDate(d.getDate() + 14);
      break;
  }
  return FI_DATE.format(d);
}

export function buildMockTaskGroups(now: Date = new Date()): ReadonlyArray<TaskGroup> {
  return seeds.map(seed => ({
    id: seed.id,
    label: seed.label,
    date: dateForGroup(seed.id, now),
    tasks: [...seed.tasks].sort(compareTasks),
  }));
}

function compareTasks(a: TaskCard, b: TaskCard): number {
  if (a.urgent !== b.urgent) return a.urgent ? -1 : 1;
  const ao = a.sortOrder ?? Number.MAX_SAFE_INTEGER;
  const bo = b.sortOrder ?? Number.MAX_SAFE_INTEGER;
  if (ao !== bo) return ao - bo;
  return accentRank(a.accent) - accentRank(b.accent);
}

function accentRank(accent: TaskCard['accent']): number {
  switch (accent) {
    case 'cta':  return 0;
    case 'warn': return 1;
    case 'info': return 2;
    case 'navy': return 3;
  }
}

const TYPE_LABELS: Record<TaskCard['type'], string> = {
  'visit-introduction':  'Tutustumiskäynti',
  'visit-reservation':   'Varausesittely',
  'open-house':          'Yleisesittely',
  'signature-pending':   'Allekirjoitus',
  'inbox-termination':   'Saapunut irtisanominen',
  'inbox-signed':        'Allekirjoitettu palautunut',
  'photo-scheduled':     'Valokuvaus',
  'renovation-approval': 'Remontti',
  'lead-callback':       'Liidi',
  'desk-list-item':      'Tiskilista',
};

/** Default row-actions per task type; aligns with SCREENS.md §04 and BACKEND.md §6. */
function actionsFor(type: TaskCard['type']): ReadonlyArray<TaskRowAction> {
  const common: ReadonlyArray<TaskRowAction> = [
    { id: 'navigate-unit', label: 'Avaa kohde Lumo Verkossa', kind: 'external',
      href: 'https://www.lumo.fi/' },
    { id: 'mark-done', label: 'Merkitse tehdyksi', kind: 'mark-done',
      confirm: { title: 'Vahvistus', body: 'Tehtävä merkitään tehdyksi. Jatka?' } },
    { id: 'cancel', label: 'Peruuta', kind: 'cancel', destructive: true,
      confirm: { title: 'Peruuta', body: 'Peruutetaanko tehtävä? Toiminto kirjataan auditiin.' } },
  ];
  switch (type) {
    case 'visit-introduction':
    case 'visit-reservation':
      return [
        common[0],
        { id: 'create-offer', label: 'Tee tarjous tästä', kind: 'create-offer' },
        { id: 'mark-done', label: 'Merkitse pidetyksi', kind: 'mark-done',
          confirm: { title: 'Käynti pidetty?', body: 'Käynti merkitään pidetyksi.' } },
        common[2],
      ];
    case 'signature-pending':
      return [
        common[0],
        { id: 'mark-done', label: 'Merkitse allekirjoitetuksi', kind: 'mark-done',
          confirm: { title: 'Vahvistus', body: 'Allekirjoitus merkitään valmiiksi.' } },
        common[2],
      ];
    case 'inbox-termination':
    case 'inbox-signed':
      return [
        { id: 'mark-done', label: 'Merkitse käsitellyksi', kind: 'mark-done',
          confirm: { title: 'Vahvistus', body: 'Tehtävä merkitään käsitellyksi.' } },
      ];
    default:
      return common;
  }
}

function initialsFrom(who: string | undefined): string {
  if (!who) return '··';
  const cleaned = who.split('·')[0].trim();
  const parts = cleaned.split(/\s+/).filter(Boolean);
  if (parts.length === 0) return '··';
  if (parts.length === 1) return parts[0].slice(0, 2).toUpperCase();
  return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
}

function phoneFrom(who: string | undefined): string | undefined {
  if (!who) return undefined;
  const match = who.match(/(\+?\d[\d\s]{5,})/);
  return match ? match[1].trim() : undefined;
}

function findCard(id: string): TaskCard | null {
  for (const seed of seeds) {
    const found = seed.tasks.find(t => t.id === id);
    if (found) return found;
  }
  return null;
}

/**
 * Build a TaskDetail for the given task id. Returns null when the id is
 * unknown — the component can show a "not found" state. Once the BACKEND
 * `/api/tasks/:id` endpoint exists, this function is replaced by a service
 * call returning the same shape.
 */
export function buildMockTaskDetail(id: string): TaskDetail | null {
  const card = findCard(id);
  if (!card) return null;

  const type = card.type;
  const customer = card.who ? {
    id: `customer-${card.id}`,
    name: card.who.split('·')[0].trim(),
    initials: initialsFrom(card.who),
    phone: phoneFrom(card.who),
  } : undefined;

  const subtitle = card.meta;

  const timeContext = card.when.note
    ? `${card.when.time} · ${card.when.note}`
    : card.when.time;

  return {
    id: card.id,
    type,
    typeLabel: card.typeLabel ?? TYPE_LABELS[type],
    accent: card.accent,
    timeContext,
    title: card.title,
    subtitle,
    customer,
    actions: actionsFor(type),
    note: {
      id: `note-${card.id}`,
      body:
        type === 'visit-introduction' || type === 'visit-reservation'
          ? 'Asiakas on muuttamassa kohti pääkaupunkiseutua. Painottaa rauhallista naapurustoa.'
          : type === 'inbox-termination'
            ? 'Asiakas vahvisti irtisanomisen sähköpostissa 30.4. Ei lisätietoja.'
            : '',
      updatedAt: '2026-05-01T08:30:00+03:00',
    },
    primaryCta: card.actions.find(a => a.primary) ?? card.actions[0] ?? {
      kind: 'navigate', label: 'Avaa kohde', primary: true,
    },
    entityRef: card.entityRef,
  };
}
