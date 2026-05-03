import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  computed,
  inject,
  input,
  signal,
} from '@angular/core';
import { takeUntilDestroyed, toObservable } from '@angular/core/rxjs-interop';
import { CurrencyPipe, DatePipe, DecimalPipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { catchError, of, switchMap } from 'rxjs';

import { TiskilistaApiService } from '../../core/services/tiskilista-api.service';
import type { TiskilistaDetailDto } from '../../core/models/tiskilista-detail.dto';

@Component({
  selector: 'app-tiskilista-detail',
  imports: [RouterLink, CurrencyPipe, DatePipe, DecimalPipe],
  template: `
    <main class="detail">
      <a class="back" routerLink="/tiskilista">← Takaisin tiskilistaan</a>

      @if (loading()) {
        <p class="status">Ladataan…</p>
      } @else if (loadError()) {
        <p class="error">Tiedon lataus epäonnistui.</p>
      } @else {
        @let t = item();
        @if (t) {
        <header>
          <h1>{{ t.osoite }}</h1>
          <p class="meta">
            @if (t.postinumero) { <span>{{ t.postinumero }} {{ t.postitoimipaikka }}</span> }
            @if (t.kaupunginosa) { <span>· {{ t.kaupunginosa }}</span> }
            @if (t.kunta) { <span>· {{ t.kunta }}</span> }
          </p>
        </header>

        <section class="hero">
          <div class="hero-cell">
            <span class="label">Vuokra</span>
            <span class="value">{{ t.vuokra | currency:'EUR':'symbol':'1.0-2' }}</span>
          </div>
          <div class="hero-cell">
            <span class="label">Tyyppi</span>
            <span class="value">{{ t.tyyppi || '—' }}</span>
          </div>
          <div class="hero-cell">
            <span class="label">Pinta-ala</span>
            <span class="value">{{ t.neliot ? (t.neliot | number:'1.0-1') + ' m²' : '—' }}</span>
          </div>
          <div class="hero-cell">
            <span class="label">Kerros</span>
            <span class="value">
              {{ t.kerros || '—' }}{{ t.kerroksia ? '/' + t.kerroksia : '' }}
            </span>
          </div>
        </section>

        <section class="actions">
          <button
            type="button"
            class="primary"
            [disabled]="!canNavigate()"
            (click)="navigateToMaps(t)"
          >Navigoi kohteeseen</button>
        </section>

        <section>
          <h2>Tilatiedot</h2>
          <dl class="grid">
            <div><dt>Tila</dt><dd>{{ t.tila || '—' }}</dd></div>
            <div><dt>Sopimustila</dt><dd>{{ t.sopimusTila || '—' }}</dd></div>
            <div><dt>Vapautuu</dt><dd>{{ t.vapautuu ? (t.vapautuu | date:'dd.MM.yyyy') : '—' }}</dd></div>
            <div><dt>Poismuutto</dt><dd>{{ t.poismuutto ? (t.poismuutto | date:'dd.MM.yyyy') : '—' }}</dd></div>
            @if (t.remonttiAlkaa || t.remonttiPaattyy) {
              <div><dt>Remontti</dt>
                <dd>
                  @if (t.remonttiAlkaa) { {{ t.remonttiAlkaa | date:'dd.MM.yyyy' }} } @else { ? }
                  –
                  @if (t.remonttiPaattyy) { {{ t.remonttiPaattyy | date:'dd.MM.yyyy' }} } @else { ? }
                </dd>
              </div>
            }
            <div><dt>Aluetoimisto</dt><dd>{{ t.aluetoimisto || '—' }}</dd></div>
            <div><dt>Markkinointialue</dt><dd>{{ t.markkinointialue || '—' }}</dd></div>
          </dl>
        </section>

        <section>
          <h2>Markkinointi</h2>
          <ul class="flags">
            <li [class.on]="t.lumoFi">Lumo.fi</li>
            <li [class.on]="t.vuokraovi">Vuokraovi</li>
            <li [class.on]="t.onKuvausTarve">Kuvaustarve</li>
          </ul>
          @if (t.lumoUrl) {
            <a class="external" [href]="t.lumoUrl" target="_blank" rel="noopener">Avaa lumo.fi ↗</a>
          }
        </section>

        <section>
          <h2>Varustelu</h2>
          <ul class="flags">
            <li [class.on]="t.hissi">Hissi</li>
            <li [class.on]="t.parveke">Parveke</li>
            <li [class.on]="t.sauna">Oma sauna</li>
            <li [class.on]="t.yhteissaUna">Yhteissauna</li>
            <li [class.on]="t.vesimittaus">Vesimittaus</li>
            <li [class.on]="t.pesula">Pesula</li>
            <li [class.on]="t.astianpesukone">Astianpesukone</li>
          </ul>
        </section>

        @if (t.muistio || t.kuvaus || t.lisaTieto) {
          <section>
            <h2>Muistiot</h2>
            @if (t.muistio) { <p class="text-block">{{ t.muistio }}</p> }
            @if (t.kuvaus) { <p class="text-block">{{ t.kuvaus }}</p> }
            @if (t.lisaTieto) { <p class="text-block">{{ t.lisaTieto }}</p> }
          </section>
        }
        }
      }
    </main>
  `,
  styleUrl: './tiskilista-detail.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TiskilistaDetailComponent {
  private readonly api = inject(TiskilistaApiService);
  private readonly destroyRef = inject(DestroyRef);

  /** Bound from the route parameter via withComponentInputBinding. */
  readonly id = input.required<string>();

  protected readonly item = signal<TiskilistaDetailDto | null>(null);
  protected readonly loading = signal<boolean>(true);
  protected readonly loadError = signal<boolean>(false);

  protected readonly canNavigate = computed(() => {
    const t = this.item();
    return t !== null && t.latitude !== null && t.longitude !== null;
  });

  constructor() {
    toObservable(this.id)
      .pipe(
        switchMap((id) => {
          this.loading.set(true);
          this.loadError.set(false);
          this.item.set(null);
          return this.api.get(id).pipe(
            catchError((err) => {
              console.error('[Tiskilista] detail load failed', err);
              this.loadError.set(true);
              return of(null);
            }),
          );
        }),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((dto) => {
        this.loading.set(false);
        if (dto) this.item.set(dto);
      });
  }

  protected navigateToMaps(t: TiskilistaDetailDto): void {
    if (t.latitude === null || t.longitude === null) return;
    const dest = `${t.latitude},${t.longitude}`;
    const isAppleDevice = /iPhone|iPad|iPod|Macintosh/i.test(navigator.userAgent);
    const url = isAppleDevice
      ? `https://maps.apple.com/?daddr=${encodeURIComponent(dest)}`
      : `https://www.google.com/maps/dir/?api=1&destination=${encodeURIComponent(dest)}`;
    window.open(url, '_blank', 'noopener');
  }
}
