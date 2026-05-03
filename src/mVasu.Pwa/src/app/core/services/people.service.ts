import { Injectable, signal } from '@angular/core';

import type {
  Person,
  PersonFilters,
  PersonRole,
} from '../models/person.dto';

/**
 * Long-tail people browser (SCREENS.md §02 + BEHAVIOR.md §3).
 *
 * Real /api/people is not wired yet — this service runs the same
 * shape against a synthetic local fixture so PeopleComponent can
 * render the list, drive the filter sheet, and exercise the recent-
 * contacts mechanic without backend dependencies. Names are clearly
 * placeholder Finnish surnames+given-names with no real customer
 * mapping (Org rule: never expose real customer identifiers).
 */

const RECENT_KEY = 'lumo-people-recents';
const RECENT_MAX = 5;

const MOCK_PEOPLE: ReadonlyArray<Person> = [
  // Tenants — current Asukkaat
  { id: 'p-001', name: 'Aalto, Eero',         initials: 'AE', role: 'tenant',
    meta: 'Asukas · Mannerheimintie 12 A 4',   city: 'Helsinki',  contractType: 'Toistaiseksi',
    tag: { label: 'Sopimus', tone: 'default' } },
  { id: 'p-002', name: 'Aro, Hilkka',          initials: 'AH', role: 'tenant',
    meta: 'Asukas · Vänrikinkatu 2',           city: 'Helsinki',  contractType: 'Toistaiseksi' },
  { id: 'p-003', name: 'Esimerkki, Eemeli',    initials: 'EE', role: 'tenant',
    meta: 'Asukas · Aleksanterinkatu 12',      city: 'Helsinki',  contractType: 'Toistaiseksi' },
  { id: 'p-004', name: 'Halonen, Inkeri',      initials: 'HI', role: 'tenant',
    meta: 'Asukas · Hämeenkatu 7',             city: 'Tampere',   contractType: 'Toistaiseksi' },
  { id: 'p-005', name: 'Heikkinen, Antti',     initials: 'HA', role: 'tenant',
    meta: 'Asukas · Kaisaniemenkatu 3',        city: 'Helsinki',  contractType: 'Määräaikainen' },
  { id: 'p-006', name: 'Holopainen, Tuomas',   initials: 'HT', role: 'tenant',
    meta: 'Asukas · Maauunintie 23 A 2',       city: 'Vantaa',    contractType: 'Toistaiseksi' },
  { id: 'p-007', name: 'Jokinen, Liisa',       initials: 'JL', role: 'tenant',
    meta: 'Asukas · Asemakuja 1 B 69',         city: 'Espoo',     contractType: 'Toistaiseksi' },
  { id: 'p-008', name: 'Järvinen, Riitta',     initials: 'JR', role: 'tenant',
    meta: 'Asukas · Ellipsikuja 2 D 8',        city: 'Espoo',     contractType: 'Toistaiseksi' },
  { id: 'p-009', name: 'Kallio, Marja',        initials: 'KM', role: 'tenant',
    meta: 'Asukas · Mannerheimintie 12 B 7',   city: 'Helsinki',  contractType: 'Toistaiseksi' },
  { id: 'p-010', name: 'Karhu, Pekka',         initials: 'KP', role: 'tenant',
    meta: 'Asukas · Tehtaankatu 8',            city: 'Helsinki',  contractType: 'Toistaiseksi',
    tag: { label: 'Päättyy 30.6.', tone: 'info' } },
  { id: 'p-011', name: 'Koivula, Saara',       initials: 'KS', role: 'tenant',
    meta: 'Asukas · Kauppakatu 14',            city: 'Lahti',     contractType: 'Toistaiseksi' },
  { id: 'p-012', name: 'Korhonen, Anna',       initials: 'KA', role: 'tenant',
    meta: 'Asukas · Kalevankatu 22',           city: 'Tampere',   contractType: 'Toistaiseksi' },
  { id: 'p-013', name: 'Laakso, Jukka',        initials: 'LJ', role: 'tenant',
    meta: 'Asukas · Mannerheimintie 12 C 11',  city: 'Helsinki',  contractType: 'Toistaiseksi' },
  { id: 'p-014', name: 'Laine, Suvi',          initials: 'LS', role: 'tenant',
    meta: 'Asukas · Itäkatu 5',                city: 'Oulu',      contractType: 'Toistaiseksi' },
  { id: 'p-015', name: 'Lehto, Kalle',         initials: 'LK', role: 'tenant',
    meta: 'Asukas · Vänrikinkatu 2 B',         city: 'Helsinki',  contractType: 'Toistaiseksi' },
  { id: 'p-016', name: 'Lindholm, Kati',       initials: 'LK', role: 'tenant',
    meta: 'Asukas · Hämeentie 99',             city: 'Helsinki',  contractType: 'Määräaikainen' },
  { id: 'p-017', name: 'Mäkelä, Olli',         initials: 'MO', role: 'tenant',
    meta: 'Asukas · Pursimiehenkatu 4',        city: 'Helsinki',  contractType: 'Toistaiseksi' },
  { id: 'p-018', name: 'Niemi, Anneli',        initials: 'NA', role: 'tenant',
    meta: 'Asukas · Asemakuja 1 B 12',         city: 'Espoo',     contractType: 'Toistaiseksi' },
  { id: 'p-019', name: 'Nieminen, Petteri',    initials: 'NP', role: 'tenant',
    meta: 'Asukas · Maauunintie 23 A 8',       city: 'Vantaa',    contractType: 'Toistaiseksi' },
  { id: 'p-020', name: 'Oksanen, Helena',      initials: 'OH', role: 'tenant',
    meta: 'Asukas · Kasarmikatu 19',           city: 'Helsinki',  contractType: 'Toistaiseksi' },
  { id: 'p-021', name: 'Peltonen, Mikko',      initials: 'PM', role: 'tenant',
    meta: 'Asukas · Aleksanterinkatu 22',      city: 'Helsinki',  contractType: 'Toistaiseksi' },
  { id: 'p-022', name: 'Rantanen, Eija',       initials: 'RE', role: 'tenant',
    meta: 'Asukas · Kapteeninkatu 7',          city: 'Helsinki',  contractType: 'Toistaiseksi' },
  { id: 'p-023', name: 'Saari, Hannu',         initials: 'SH', role: 'tenant',
    meta: 'Asukas · Itäkatu 18',               city: 'Oulu',      contractType: 'Toistaiseksi' },
  { id: 'p-024', name: 'Salminen, Riikka',     initials: 'SR', role: 'tenant',
    meta: 'Asukas · Kauppakatu 5',             city: 'Lahti',     contractType: 'Toistaiseksi' },
  { id: 'p-025', name: 'Salonen, Jari',        initials: 'SJ', role: 'tenant',
    meta: 'Asukas · Hämeenkatu 12',            city: 'Tampere',   contractType: 'Toistaiseksi' },
  { id: 'p-026', name: 'Toivonen, Sinikka',    initials: 'TS', role: 'tenant',
    meta: 'Asukas · Tehtaankatu 14',           city: 'Helsinki',  contractType: 'Toistaiseksi' },
  { id: 'p-027', name: 'Tuominen, Heikki',     initials: 'TH', role: 'tenant',
    meta: 'Asukas · Tampereenkatu 3',          city: 'Tampere',   contractType: 'Toistaiseksi' },
  { id: 'p-028', name: 'Virtanen, Maija',      initials: 'VM', role: 'tenant',
    meta: 'Asukas · Mannerheimintie 12 D 2',   city: 'Helsinki',  contractType: 'Määräaikainen',
    tag: { label: 'Allekirjoitus', tone: 'cta' } },

  // Applicants — Hakijat (some with offers, some without)
  { id: 'p-101', name: 'Ahonen, Matti',        initials: 'AM', role: 'applicant',
    meta: 'Hakija · tarjous Mannerheimintie 12 A 4 · odottaa allekirjoitusta',
                                               city: 'Helsinki',
    tag: { label: 'Allekirjoitus', tone: 'cta' } },
  { id: 'p-102', name: 'Enckell, Tiia',        initials: 'ET', role: 'applicant',
    meta: 'Hakija · max 555 €/kk',             city: 'Lappeenranta',
    tag: { label: 'Uusi', tone: 'info' } },
  { id: 'p-103', name: 'Halme, Iida',          initials: 'HI', role: 'applicant',
    meta: 'Hakija · Kaksio · max 800 €/kk',    city: 'Oulu' },
  { id: 'p-104', name: 'Heinonen, Pasi',       initials: 'HP', role: 'applicant',
    meta: 'Hakija · Kolmio · max 1100 €/kk',   city: 'Espoo' },
  { id: 'p-105', name: 'Helmikuu, Helmiä',     initials: 'HH', role: 'applicant',
    meta: 'Hakija · Kaksio · max 750 €/kk',    city: 'Espoo' },
  { id: 'p-106', name: 'Huhtikuu, Heikki',     initials: 'HH', role: 'applicant',
    meta: 'Hakija · Yksiö · max 700 €/kk',     city: 'Helsinki',
    tag: { label: 'Tutustumiskäynti tänään', tone: 'info' } },
  { id: 'p-107', name: 'Ikonen, Ansa',         initials: 'IA', role: 'applicant',
    meta: 'Hakija · Verkkokauppa-tilaus',      city: 'Espoo',
    tag: { label: 'Verkkokauppa', tone: 'info' } },
  { id: 'p-108', name: 'Jätkä, Saara',         initials: 'JS', role: 'applicant',
    meta: 'Hakija · Yksiö · max 700 €/kk',     city: 'Helsinki' },
  { id: 'p-109', name: 'Koivuranta, Kaija',    initials: 'KK', role: 'applicant',
    meta: 'Hakija · Kaksio · max 800 €/kk',    city: 'Oulu' },
  { id: 'p-110', name: 'Lampi, Santeri',       initials: 'LS', role: 'applicant',
    meta: 'Hakija · Kolmio · max 1200 €/kk',   city: 'Tampere' },
  { id: 'p-111', name: 'Lehtinen, Aino',       initials: 'LA', role: 'applicant',
    meta: 'Hakija · Kaksio',                   city: 'Helsinki' },
  { id: 'p-112', name: 'Pajunen, Hanna',       initials: 'PH', role: 'applicant',
    meta: 'Hakija · Tarjous odottaa',          city: 'Helsinki',
    tag: { label: 'Allekirjoitus', tone: 'cta' } },
  { id: 'p-113', name: 'Suominen, Lauri',      initials: 'SL', role: 'applicant',
    meta: 'Hakija · Yksiö · max 650 €/kk',     city: 'Vantaa' },
  { id: 'p-114', name: 'Testaaja, Maaliskuu',  initials: 'TM', role: 'applicant',
    meta: 'Hakija · Kolmio · max 950 €/kk',    city: 'Mäntsälä' },
  { id: 'p-115', name: 'Tähtinen, Veera',      initials: 'TV', role: 'applicant',
    meta: 'Hakija · Kaksio',                   city: 'Espoo' },
  { id: 'p-116', name: 'Watson, Juan',         initials: 'WJ', role: 'applicant',
    meta: 'Hakija · Verkkokauppa-tilaus',      city: 'Espoo',
    tag: { label: 'Verkkokauppa', tone: 'info' } },

  // Former tenants — Entiset
  { id: 'p-201', name: 'Aurinkoinen, Tuutikki',initials: 'AT', role: 'former',
    meta: 'Päättynyt · Aleksanterinkatu 12',   city: 'Helsinki',
    tag: { label: 'Päättynyt 31.3.', tone: 'default' } },
  { id: 'p-202', name: 'Granberg, Satu',       initials: 'GS', role: 'former',
    meta: 'Päättynyt · Hämeenkatu 7 B',        city: 'Tampere' },
  { id: 'p-203', name: 'Hellberg, Ari',        initials: 'HA', role: 'former',
    meta: 'Päättynyt · Kasarmikatu 4',         city: 'Helsinki' },
  { id: 'p-204', name: 'Mononen, Risto',       initials: 'MR', role: 'former',
    meta: 'Päättynyt · Itäkatu 30',            city: 'Oulu' },
  { id: 'p-205', name: 'Ojanen, Tarja',        initials: 'OT', role: 'former',
    meta: 'Päättynyt · Tampereenkatu 9',       city: 'Tampere' },
  { id: 'p-206', name: 'Riihimäki, Ulla',      initials: 'RU', role: 'former',
    meta: 'Päättynyt · Vänrikinkatu 5',        city: 'Helsinki' },
];

const FI_COLLATOR = new Intl.Collator('fi-FI', { sensitivity: 'base' });

@Injectable({ providedIn: 'root' })
export class PeopleService {
  /** Last 5 person ids the user opened from this tab. */
  readonly recentIds = signal<ReadonlyArray<string>>(this.readRecents());

  /** Distinct city set across the dataset; used to populate the filter sheet. */
  readonly cities: ReadonlyArray<string> = Array.from(
    new Set(MOCK_PEOPLE.map(p => p.city).filter((c): c is string => !!c)),
  ).sort(FI_COLLATOR.compare);

  /**
   * List people matching `query` and `filters`. Returns alphabetically
   * sorted (Finnish locale) so Ä/Ö come last as expected. The real
   * /api/people will paginate; for now ~50 entries fit comfortably.
   */
  list(query: string, filters: PersonFilters): ReadonlyArray<Person> {
    const needle = query.trim().toLowerCase();
    return MOCK_PEOPLE
      .filter(p => this.matchFilters(p, filters))
      .filter(p => this.matchQuery(p, needle))
      .slice()
      .sort((a, b) => FI_COLLATOR.compare(a.name, b.name));
  }

  byId(id: string): Person | undefined {
    return MOCK_PEOPLE.find(p => p.id === id);
  }

  /** Resolve recent ids back to Person objects, preserving recency order. */
  recentPeople(): ReadonlyArray<Person> {
    return this.recentIds()
      .map(id => this.byId(id))
      .filter((p): p is Person => !!p);
  }

  pushRecent(id: string): void {
    const next = [id, ...this.recentIds().filter(x => x !== id)].slice(0, RECENT_MAX);
    this.recentIds.set(next);
    this.persistRecents(next);
  }

  /**
   * Highlight matched substring in `text` against the (case-insensitive)
   * needle. Returns alternating plain / mark segments — the component
   * renders without dangerouslySetInnerHTML.
   */
  highlight(text: string, needle: string): ReadonlyArray<{ readonly text: string; readonly mark: boolean }> {
    const trimmed = needle.trim();
    if (!trimmed) return [{ text, mark: false }];
    const segments: Array<{ text: string; mark: boolean }> = [];
    const lowerText = text.toLowerCase();
    const lowerNeedle = trimmed.toLowerCase();
    let cursor = 0;
    let idx = lowerText.indexOf(lowerNeedle, cursor);
    while (idx !== -1) {
      if (idx > cursor) segments.push({ text: text.slice(cursor, idx), mark: false });
      segments.push({ text: text.slice(idx, idx + trimmed.length), mark: true });
      cursor = idx + trimmed.length;
      idx = lowerText.indexOf(lowerNeedle, cursor);
    }
    if (cursor < text.length) segments.push({ text: text.slice(cursor), mark: false });
    return segments;
  }

  private matchQuery(p: Person, needle: string): boolean {
    if (!needle) return true;
    return p.name.toLowerCase().includes(needle)
        || p.meta.toLowerCase().includes(needle)
        || (p.phone?.includes(needle) ?? false)
        || (p.email?.toLowerCase().includes(needle) ?? false);
  }

  private matchFilters(p: Person, f: PersonFilters): boolean {
    if (f.role && p.role !== f.role) return false;
    if (f.city && p.city !== f.city) return false;
    return true;
  }

  private readRecents(): ReadonlyArray<string> {
    if (typeof sessionStorage === 'undefined') return [];
    try {
      const raw = sessionStorage.getItem(RECENT_KEY);
      if (!raw) return [];
      const parsed = JSON.parse(raw);
      return Array.isArray(parsed) ? parsed.filter((v): v is string => typeof v === 'string') : [];
    } catch {
      return [];
    }
  }

  private persistRecents(value: ReadonlyArray<string>): void {
    if (typeof sessionStorage === 'undefined') return;
    try {
      sessionStorage.setItem(RECENT_KEY, JSON.stringify(value));
    } catch {
      // ignore quota / privacy mode
    }
  }
}
