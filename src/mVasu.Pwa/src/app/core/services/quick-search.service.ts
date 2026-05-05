import { Injectable, signal } from '@angular/core';

/**
 * Long-tail global search across all XAF entity types and quick actions.
 * Mirrors BACKEND.md §5 `/api/search?q=…&limit=5` and SCREENS.md §05.
 *
 * Backend wiring is deferred — this service runs the same shape against
 * a local mock fixture so QuickSearchComponent can be styled and the
 * keyboard interactions tuned independently of the API.
 */

export type SearchGroupId = 'units' | 'people' | 'contracts' | 'actions';

export interface SearchHit {
  readonly id: string;
  readonly group: SearchGroupId;
  /** Raw title — frontend renders the highlight; do not pre-mark here. */
  readonly title: string;
  readonly meta: string;
  /** DevExtreme icon name shown to the left of each row. */
  readonly icon: string;
  /** Internal route navigated to on Enter / tap. Optional for "Tulossa" rows. */
  readonly navigate?: string;
}

export interface SearchGroup {
  readonly id: SearchGroupId;
  readonly label: string;
  readonly hits: ReadonlyArray<SearchHit>;
}

const RECENT_KEY = 'lumo-quick-search-recents';
const RECENT_MAX = 5;
const MIN_QUERY_LENGTH = 2;

const GROUP_LABELS: Record<SearchGroupId, string> = {
  units:     'Kohteet',
  people:    'Asukkaat',
  contracts: 'Sopimukset',
  actions:   'Toiminnot',
};

const MOCK_HITS: SearchHit[] = [
  // Units (XAF Asuinhuoneisto / Tiskilista)
  { id: 'unit-1', group: 'units', icon: 'home',
    title: 'Mannerheimintie 12 A 4',
    meta: '2 h · 47 m² · vapaa 1.7.',
    navigate: '/tiskilista' },
  { id: 'unit-2', group: 'units', icon: 'home',
    title: 'Mannerheimintie 12 B 7',
    meta: '3 h · 68 m² · varattu' },
  { id: 'unit-3', group: 'units', icon: 'home',
    title: 'Aleksanterinkatu 12',
    meta: '4 h+s · 95 m² · vapaa 15.6.' },
  { id: 'unit-4', group: 'units', icon: 'home',
    title: 'Maauunintie 23 A 2, 01450 Vantaa',
    meta: '1 h+kk · 32 m² · esittely huomenna 14:00' },
  { id: 'unit-5', group: 'units', icon: 'home',
    title: 'Asemakuja 1 B 69, 02770 Espoo',
    meta: '2 h+k · 54 m² · varattu' },
  { id: 'unit-6', group: 'units', icon: 'home',
    title: 'Vänrikinkatu 2',
    meta: '2 h+k · 48,5 m² · sopimus odottaa' },

  // People (XAF Henkilo / Hakija)
  { id: 'person-1', group: 'people', icon: 'user',
    title: 'Asiakas_001',
    meta: 'Hakija · Vänrikinkatu 2' },
  { id: 'person-2', group: 'people', icon: 'user',
    title: 'Asiakas_002',
    meta: 'Hakija · Mannerheimintie 12 A 4' },
  { id: 'person-3', group: 'people', icon: 'user',
    title: 'Asiakas_003',
    meta: 'Asukas · Vänrikinkatu 2' },
  { id: 'person-4', group: 'people', icon: 'user',
    title: 'Asiakas_004',
    meta: 'Päähakija · Asemakuja 1 B 69' },
  { id: 'person-5', group: 'people', icon: 'user',
    title: 'Asiakas_005',
    meta: 'Liidi · max 555 €/kk · Lappeenranta' },

  // Contracts (XAF Sopimus / Tarjous)
  { id: 'contract-1', group: 'contracts', icon: 'doc',
    title: 'Tarjous SOP-PLACEHOLDER · Vänrikinkatu 2',
    meta: 'Allekirjoitusta odottaa · 3 pv' },
  { id: 'contract-2', group: 'contracts', icon: 'doc',
    title: 'Sopimus 100029',
    meta: 'Voimassa · Aleksanterinkatu 12 · Päättyy 31.1.2027' },
  { id: 'contract-3', group: 'contracts', icon: 'doc',
    title: 'Irtisanominen 30.4.',
    meta: 'Käsittelyä odottaa · Vänrikinkatu 2' },

  // Actions (static catalogue)
  { id: 'action-new-offer', group: 'actions', icon: 'plus',
    title: 'Tee uusi tarjous',
    meta: 'Aloita tyhjältä lomakkeelta' },
  { id: 'action-new-visit', group: 'actions', icon: 'event',
    title: 'Varaa esittely',
    meta: 'Sovi aika hakijan kanssa' },
  { id: 'action-new-task', group: 'actions', icon: 'check',
    title: 'Lisää uusi tehtävä',
    meta: 'Luo Tehtava xVasuSecuritySystemUser:lle',
    navigate: '/tasks' },
];

@Injectable({ providedIn: 'root' })
export class QuickSearchService {
  /** Last 5 user queries; persisted across the tab session only. */
  readonly recentQueries = signal<ReadonlyArray<string>>(this.readRecents());

  /**
   * Synchronous mock search. Real BACKEND.md /api/search will be an
   * HTTP roundtrip; the service signature stays the same so the
   * component does not change when the wiring flips.
   */
  search(query: string, limitPerGroup = 5): ReadonlyArray<SearchGroup> {
    const q = query.trim();
    if (q.length < MIN_QUERY_LENGTH) return [];
    const needle = q.toLowerCase();

    const groups: SearchGroupId[] = ['units', 'people', 'contracts', 'actions'];
    return groups
      .map<SearchGroup>(id => ({
        id,
        label: GROUP_LABELS[id],
        hits: MOCK_HITS
          .filter(hit => hit.group === id)
          .filter(hit =>
            hit.title.toLowerCase().includes(needle) ||
            hit.meta.toLowerCase().includes(needle))
          .slice(0, limitPerGroup),
      }))
      .filter(group => group.hits.length > 0);
  }

  pushRecentQuery(query: string): void {
    const trimmed = query.trim();
    if (trimmed.length < MIN_QUERY_LENGTH) return;
    const next = [trimmed, ...this.recentQueries().filter(q => q !== trimmed)]
      .slice(0, RECENT_MAX);
    this.recentQueries.set(next);
    this.persistRecents(next);
  }

  clearRecents(): void {
    this.recentQueries.set([]);
    this.persistRecents([]);
  }

  /**
   * Splits `text` into alternating plain / highlighted segments around the
   * (case-insensitive) `needle`. Empty needle returns the original text as
   * a single plain segment.
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
      if (idx > cursor) {
        segments.push({ text: text.slice(cursor, idx), mark: false });
      }
      segments.push({ text: text.slice(idx, idx + trimmed.length), mark: true });
      cursor = idx + trimmed.length;
      idx = lowerText.indexOf(lowerNeedle, cursor);
    }
    if (cursor < text.length) {
      segments.push({ text: text.slice(cursor), mark: false });
    }
    return segments;
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
      // Quota / privacy mode — fall back to in-memory only.
    }
  }
}
