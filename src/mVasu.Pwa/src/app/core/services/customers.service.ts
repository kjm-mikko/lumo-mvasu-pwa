import { Injectable, signal } from '@angular/core';

import type { Customer } from '../models/customer.dto';

/**
 * Asiakkaat browser support — ancillary state that lives outside
 * `CustomersApiService.list()`. The list itself comes straight from
 * `GET /api/customers` (XPO-backed → xVasu.Data.Asma.Asiakas hierarchy).
 *
 * Three pieces remain client-side:
 *
 *  - `cities` — a curated top-N city list used by the filter sheet's
 *    quick-pick chips. The list is hardcoded for now; the filter still
 *    accepts free-text via the typeahead, so kuntia outside this set
 *    remain reachable. TODO: replace with `GET /api/customers/cities`
 *    distinct-values endpoint when we want this list to track the
 *    actual customer master.
 *
 *  - `recentIds` — sessionStorage of recently opened customer ids.
 *    Currently a write-only data sink: the row click pushes an id, but
 *    the "Viimeksi avatut" UI is hidden until a `/api/customers/by-ids`
 *    endpoint is added so we can rehydrate the rows server-side.
 *
 *  - `highlight` — pure utility for marking the search needle in row
 *    text. No data dependency.
 */

const RECENT_KEY = 'lumo-customers-recents';
const RECENT_MAX = 5;

/**
 * Top kuntia in Lumo's portfolio surfaced as quick-pick chips in the
 * filter sheet. Chosen by population in Lumo's typical operating area.
 * Cities outside this set are reachable via the dx-select-box typeahead
 * below the chips (the typeahead currently shows the same list — see
 * the TODO at the top of this file for the dynamic-list follow-up).
 */
const TOP_CITIES: ReadonlyArray<string> = [
  'Helsinki', 'Tampere', 'Espoo', 'Vantaa', 'Oulu',
  'Turku', 'Jyväskylä', 'Lahti', 'Kuopio', 'Pori',
  'Joensuu', 'Lappeenranta', 'Hämeenlinna', 'Vaasa', 'Seinäjoki',
  'Rovaniemi', 'Mikkeli', 'Kotka', 'Salo', 'Porvoo',
];

@Injectable({ providedIn: 'root' })
export class CustomersService {
  readonly recentIds = signal<ReadonlyArray<string>>(this.readRecents());

  readonly cities: ReadonlyArray<string> = TOP_CITIES;

  /**
   * Returns hydrated rows for the recently opened ids. Currently a
   * stub: until `/api/customers/by-ids` exists we cannot resolve an
   * id back to a row outside the current page, so the row UI is
   * hidden. The id sink (`pushRecent`) keeps recording so the data
   * is ready when the endpoint lands.
   */
  recentCustomers(): ReadonlyArray<Customer> {
    return [];
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
