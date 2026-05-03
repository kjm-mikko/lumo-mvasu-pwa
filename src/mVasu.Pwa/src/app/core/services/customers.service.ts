import { Injectable, signal } from '@angular/core';

import {
  totalCount,
  type Customer,
  type CustomerCounts,
  type CustomerFilters,
  type CustomerRelationFilter,
} from '../models/customer.dto';

/**
 * Asiakkaat browser (SCREENS.md §02 + Lumo domain). Operates on the
 * `xVasu.Data.Asma.Henkilo` / `Yritys` / `Yhteyshenkilo` family —
 * a customer's role is derived from related entities (Hakemus,
 * SopimusVaraus, Sopimus, Tarjous, Esittely / Tutustumiskaynti).
 *
 * Real `/api/customers` is not wired yet — this service runs the same
 * shape against a synthetic local fixture so CustomersComponent can
 * render rich rows, drive the filter sheet, and exercise the recent-
 * customers mechanic without backend dependencies.
 *
 * Names are deliberately synthetic (Org rule: never expose real
 * customer identifiers).
 */

const RECENT_KEY = 'lumo-customers-recents';
const RECENT_MAX = 5;

const COUNTS_ZERO: CustomerCounts = {
  applications: 0, reservations: 0, contracts: 0, offers: 0, showings: 0,
};

function counts(partial: Partial<CustomerCounts>): CustomerCounts {
  return { ...COUNTS_ZERO, ...partial };
}

const MOCK_CUSTOMERS: ReadonlyArray<Customer> = [
  // ── Henkilö-asiakkaat (active tenants & various) ───────────────────
  { id: 'c-001', type: 'person', firstName: 'Eero',     lastName: 'Aalto',
    displayName: 'Aalto, Eero',         initials: 'AE',
    counts: counts({ contracts: 1 }),
    primaryAddress: 'Mannerheimintie 12 A 4', city: 'Helsinki' },

  { id: 'c-002', type: 'person', firstName: 'Hilkka',   lastName: 'Aro',
    displayName: 'Aro, Hilkka',         initials: 'AH',
    counts: counts({ contracts: 1, applications: 1 }),
    primaryAddress: 'Vänrikinkatu 2',   city: 'Helsinki',
    tag: { label: 'Etsii uutta', tone: 'info' } },

  { id: 'c-003', type: 'person', firstName: 'Eemeli',   lastName: 'Esimerkki',
    displayName: 'Esimerkki, Eemeli',   initials: 'EE',
    counts: counts({ contracts: 1 }),
    primaryAddress: 'Aleksanterinkatu 12', city: 'Helsinki' },

  { id: 'c-004', type: 'person', firstName: 'Inkeri',   lastName: 'Halonen',
    displayName: 'Halonen, Inkeri',     initials: 'HI',
    counts: counts({ contracts: 1 }),
    primaryAddress: 'Hämeenkatu 7',     city: 'Tampere' },

  { id: 'c-005', type: 'person', firstName: 'Antti',    lastName: 'Heikkinen',
    displayName: 'Heikkinen, Antti',    initials: 'HA',
    counts: counts({ contracts: 1 }),
    primaryAddress: 'Kaisaniemenkatu 3', city: 'Helsinki',
    tag: { label: 'Päättyy 30.6.', tone: 'info' } },

  { id: 'c-006', type: 'person', firstName: 'Tuomas',   lastName: 'Holopainen',
    displayName: 'Holopainen, Tuomas',  initials: 'HT',
    counts: counts({ contracts: 1, showings: 1 }),
    primaryAddress: 'Maauunintie 23 A 2', city: 'Vantaa' },

  { id: 'c-007', type: 'person', firstName: 'Liisa',    lastName: 'Jokinen',
    displayName: 'Jokinen, Liisa',      initials: 'JL',
    counts: counts({ contracts: 1, reservations: 1 }),
    primaryAddress: 'Asemakuja 1 B 69', city: 'Espoo',
    tag: { label: 'Varaus aktiivinen', tone: 'info' } },

  { id: 'c-008', type: 'person', firstName: 'Marja',    lastName: 'Kallio',
    displayName: 'Kallio, Marja',       initials: 'KM',
    counts: counts({ contracts: 1 }),
    primaryAddress: 'Mannerheimintie 12 B 7', city: 'Helsinki' },

  { id: 'c-009', type: 'person', firstName: 'Pekka',    lastName: 'Karhu',
    displayName: 'Karhu, Pekka',        initials: 'KP',
    counts: counts({ contracts: 1 }),
    primaryAddress: 'Tehtaankatu 8',    city: 'Helsinki' },

  { id: 'c-010', type: 'person', firstName: 'Saara',    lastName: 'Koivula',
    displayName: 'Koivula, Saara',      initials: 'KS',
    counts: counts({ contracts: 1, offers: 1 }),
    primaryAddress: 'Kauppakatu 14',    city: 'Lahti',
    tag: { label: 'Allekirjoitus', tone: 'cta' } },

  { id: 'c-011', type: 'person', firstName: 'Anna',     lastName: 'Korhonen',
    displayName: 'Korhonen, Anna',      initials: 'KA',
    counts: counts({ contracts: 1 }),
    primaryAddress: 'Kalevankatu 22',   city: 'Tampere' },

  { id: 'c-012', type: 'person', firstName: 'Jukka',    lastName: 'Laakso',
    displayName: 'Laakso, Jukka',       initials: 'LJ',
    counts: counts({ contracts: 1 }),
    primaryAddress: 'Mannerheimintie 12 C 11', city: 'Helsinki' },

  { id: 'c-013', type: 'person', firstName: 'Olli',     lastName: 'Mäkelä',
    displayName: 'Mäkelä, Olli',        initials: 'MO',
    counts: counts({ contracts: 1, reservations: 1 }),
    primaryAddress: 'Pursimiehenkatu 4', city: 'Helsinki' },

  { id: 'c-014', type: 'person', firstName: 'Anneli',   lastName: 'Niemi',
    displayName: 'Niemi, Anneli',       initials: 'NA',
    counts: counts({ contracts: 1 }),
    primaryAddress: 'Asemakuja 1 B 12', city: 'Espoo' },

  { id: 'c-015', type: 'person', firstName: 'Helena',   lastName: 'Oksanen',
    displayName: 'Oksanen, Helena',     initials: 'OH',
    counts: counts({ contracts: 1 }),
    primaryAddress: 'Kasarmikatu 19',   city: 'Helsinki' },

  // Pure applicants — no contract yet
  { id: 'c-101', type: 'person', firstName: 'Matti',    lastName: 'Ahonen',
    displayName: 'Ahonen, Matti',       initials: 'AM',
    counts: counts({ applications: 1, offers: 1 }),
    city: 'Helsinki',
    tag: { label: 'Allekirjoitus', tone: 'cta' } },

  { id: 'c-102', type: 'person', firstName: 'Tiia',     lastName: 'Enckell',
    displayName: 'Enckell, Tiia',       initials: 'ET',
    counts: counts({ applications: 1 }),
    city: 'Lappeenranta',
    tag: { label: 'Uusi', tone: 'info' } },

  { id: 'c-103', type: 'person', firstName: 'Iida',     lastName: 'Halme',
    displayName: 'Halme, Iida',         initials: 'HI',
    counts: counts({ applications: 1, showings: 1 }),
    city: 'Oulu',
    tag: { label: 'Tutustumiskäynti tänään', tone: 'info' } },

  { id: 'c-104', type: 'person', firstName: 'Pasi',     lastName: 'Heinonen',
    displayName: 'Heinonen, Pasi',      initials: 'HP',
    counts: counts({ applications: 1, showings: 1 }),
    city: 'Espoo' },

  { id: 'c-105', type: 'person', firstName: 'Heikki',   lastName: 'Huhtikuu',
    displayName: 'Huhtikuu, Heikki',    initials: 'HH',
    counts: counts({ applications: 1, showings: 2 }),
    city: 'Helsinki' },

  { id: 'c-106', type: 'person', firstName: 'Ansa',     lastName: 'Ikonen',
    displayName: 'Ikonen, Ansa',        initials: 'IA',
    counts: counts({ reservations: 1, offers: 1 }),
    city: 'Espoo',
    tag: { label: 'Verkkokauppa', tone: 'info' } },

  { id: 'c-107', type: 'person', firstName: 'Saara',    lastName: 'Jätkä',
    displayName: 'Jätkä, Saara',        initials: 'JS',
    counts: counts({ applications: 1 }),
    city: 'Helsinki' },

  { id: 'c-108', type: 'person', firstName: 'Kaija',    lastName: 'Koivuranta',
    displayName: 'Koivuranta, Kaija',   initials: 'KK',
    counts: counts({ applications: 1 }),
    city: 'Oulu' },

  { id: 'c-109', type: 'person', firstName: 'Hanna',    lastName: 'Pajunen',
    displayName: 'Pajunen, Hanna',      initials: 'PH',
    counts: counts({ applications: 1, offers: 1 }),
    city: 'Helsinki',
    tag: { label: 'Allekirjoitus', tone: 'cta' } },

  { id: 'c-110', type: 'person', firstName: 'Maaliskuu', lastName: 'Testaaja',
    displayName: 'Testaaja, Maaliskuu', initials: 'TM',
    counts: counts({ applications: 1 }),
    city: 'Mäntsälä' },

  // Former tenants — had contract, no longer
  { id: 'c-201', type: 'person', firstName: 'Tuutikki', lastName: 'Aurinkoinen',
    displayName: 'Aurinkoinen, Tuutikki', initials: 'AT',
    counts: counts({}),                         // no relations
    city: 'Helsinki',
    tag: { label: 'Päättynyt 31.3.', tone: 'default' } },

  { id: 'c-202', type: 'person', firstName: 'Satu',     lastName: 'Granberg',
    displayName: 'Granberg, Satu',      initials: 'GS',
    counts: counts({}),
    city: 'Tampere',
    tag: { label: 'Päättynyt', tone: 'default' } },

  // Contact-only — rare edge case (no relations at all)
  { id: 'c-301', type: 'person', firstName: 'Kirjattu', lastName: 'Yhteystieto',
    displayName: 'Yhteystieto, Kirjattu', initials: 'YK',
    counts: counts({}),
    city: 'Helsinki' },

  // ── Yritys-asiakkaat (companies on commercial contracts) ──────────
  { id: 'c-y01', type: 'company', companyName: 'Lumo Tekniikka Oy', businessId: '2345678-9',
    displayName: 'Lumo Tekniikka Oy',   initials: 'LT',
    counts: counts({ contracts: 1 }),
    primaryAddress: 'Toimitilakuja 5',  city: 'Helsinki' },

  { id: 'c-y02', type: 'company', companyName: 'Esimerkki Oy', businessId: '1112223-4',
    displayName: 'Esimerkki Oy',        initials: 'EO',
    counts: counts({ contracts: 1, applications: 1 }),
    primaryAddress: 'Yritystie 12',     city: 'Espoo' },

  { id: 'c-y03', type: 'company', companyName: 'Testaaja Asunnot Oy', businessId: '4456778-1',
    displayName: 'Testaaja Asunnot Oy', initials: 'TA',
    counts: counts({ applications: 1, offers: 1 }),
    city: 'Tampere',
    tag: { label: 'Toimitilatarjous', tone: 'cta' } },

  { id: 'c-y04', type: 'company', companyName: 'Mallikohde Oyj', businessId: '5567889-3',
    displayName: 'Mallikohde Oyj',      initials: 'MO',
    counts: counts({ contracts: 2 }),
    primaryAddress: 'Hämeenkatu 22',    city: 'Tampere' },

  { id: 'c-y05', type: 'company', companyName: 'Placeholder Yritys Oy',
    displayName: 'Placeholder Yritys Oy', initials: 'PY',
    counts: counts({}),
    city: 'Helsinki' },

  // ── Yhteyshenkilö-asiakkaat (linked to a Yritys) ──────────────────
  { id: 'c-yh01', type: 'contact-person', firstName: 'Maija', lastName: 'Virtanen',
    displayName: 'Virtanen, Maija',     initials: 'VM',
    parentCompanyId: 'c-y01', parentCompanyName: 'Lumo Tekniikka Oy',
    counts: counts({}),
    city: 'Helsinki', phone: '044 PLACEHOLDER',
    tag: { label: 'Päätösoikeus', tone: 'default' } },

  { id: 'c-yh02', type: 'contact-person', firstName: 'Pekka', lastName: 'Salo',
    displayName: 'Salo, Pekka',         initials: 'SP',
    parentCompanyId: 'c-y02', parentCompanyName: 'Esimerkki Oy',
    counts: counts({ applications: 1 }),
    city: 'Espoo' },

  { id: 'c-yh03', type: 'contact-person', firstName: 'Juhani', lastName: 'Lampila',
    displayName: 'Lampila, Juhani',     initials: 'LJ',
    parentCompanyId: 'c-y03', parentCompanyName: 'Testaaja Asunnot Oy',
    counts: counts({ offers: 1 }),
    city: 'Tampere',
    tag: { label: 'Allekirjoitus', tone: 'cta' } },

  { id: 'c-yh04', type: 'contact-person', firstName: 'Eeva', lastName: 'Mäki',
    displayName: 'Mäki, Eeva',          initials: 'ME',
    parentCompanyId: 'c-y04', parentCompanyName: 'Mallikohde Oyj',
    counts: counts({}),
    city: 'Tampere' },

  { id: 'c-yh05', type: 'contact-person', firstName: 'Kalle', lastName: 'Niinikoski',
    displayName: 'Niinikoski, Kalle',   initials: 'NK',
    parentCompanyId: 'c-y04', parentCompanyName: 'Mallikohde Oyj',
    counts: counts({}),
    city: 'Tampere' },
];

const FI_COLLATOR = new Intl.Collator('fi-FI', { sensitivity: 'base' });

@Injectable({ providedIn: 'root' })
export class CustomersService {
  readonly recentIds = signal<ReadonlyArray<string>>(this.readRecents());

  readonly cities: ReadonlyArray<string> = Array.from(
    new Set(MOCK_CUSTOMERS.map(c => c.city).filter((c): c is string => !!c)),
  ).sort(FI_COLLATOR.compare);

  list(query: string, filters: CustomerFilters): ReadonlyArray<Customer> {
    const needle = query.trim().toLowerCase();
    return MOCK_CUSTOMERS
      .filter(c => this.matchFilters(c, filters))
      .filter(c => this.matchQuery(c, needle))
      .slice()
      .sort((a, b) => FI_COLLATOR.compare(a.displayName, b.displayName));
  }

  byId(id: string): Customer | undefined {
    return MOCK_CUSTOMERS.find(c => c.id === id);
  }

  recentCustomers(): ReadonlyArray<Customer> {
    return this.recentIds()
      .map(id => this.byId(id))
      .filter((c): c is Customer => !!c);
  }

  pushRecent(id: string): void {
    const next = [id, ...this.recentIds().filter(x => x !== id)].slice(0, RECENT_MAX);
    this.recentIds.set(next);
    this.persistRecents(next);
  }

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

  private matchQuery(c: Customer, needle: string): boolean {
    if (!needle) return true;
    if (c.displayName.toLowerCase().includes(needle)) return true;
    if (c.primaryAddress?.toLowerCase().includes(needle)) return true;
    if (c.city?.toLowerCase().includes(needle)) return true;
    if (c.phone?.includes(needle)) return true;
    if (c.email?.toLowerCase().includes(needle)) return true;
    if (c.type === 'company' && c.businessId?.toLowerCase().includes(needle)) return true;
    if (c.type === 'contact-person' && c.parentCompanyName.toLowerCase().includes(needle)) return true;
    return false;
  }

  private matchFilters(c: Customer, f: CustomerFilters): boolean {
    if (f.type && c.type !== f.type) return false;
    if (f.city && c.city !== f.city) return false;
    if (f.relation && !this.matchRelation(c, f.relation)) return false;
    return true;
  }

  private matchRelation(c: Customer, relation: CustomerRelationFilter): boolean {
    switch (relation) {
      case 'has-contract':    return c.counts.contracts > 0;
      case 'has-application': return c.counts.applications > 0;
      case 'has-offer':       return c.counts.offers > 0;
      case 'has-showing':     return c.counts.showings > 0;
      case 'no-relations':    return totalCount(c.counts) === 0;
    }
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
      // ignore
    }
  }
}
