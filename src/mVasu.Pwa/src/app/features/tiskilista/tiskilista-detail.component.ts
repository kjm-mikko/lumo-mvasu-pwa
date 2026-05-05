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
import { DxToastModule } from 'devextreme-angular/ui/toast';

import { TiskilistaApiService } from '../../core/services/tiskilista-api.service';
import type { TiskilistaDetailDto } from '../../core/models/tiskilista-detail.dto';

type ToastType = 'info' | 'success' | 'warning' | 'error';
interface ToastState {
  readonly visible: boolean;
  readonly message: string;
  readonly type: ToastType;
}
const TOAST_HIDDEN: ToastState = { visible: false, message: '', type: 'info' };

@Component({
  selector: 'app-tiskilista-detail',
  imports: [DxToastModule, RouterLink, CurrencyPipe, DatePipe, DecimalPipe],
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
            <div><dt>Laji</dt><dd>{{ t.laji || '—' }}</dd></div>
            <div><dt>Vapautuu</dt><dd>{{ t.vapautuu ? (t.vapautuu | date:'dd.MM.yyyy') : '—' }}</dd></div>
            <div><dt>Poismuutto</dt><dd>{{ t.poismuutto ? (t.poismuutto | date:'dd.MM.yyyy') : '—' }}</dd></div>
            @if (t.vapautuuAsiakkaalta) {
              <div><dt>Vapautuu asiakkaalta</dt><dd>{{ t.vapautuuAsiakkaalta | date:'dd.MM.yyyy' }}</dd></div>
            }
            @if (t.remonttiAlkaa || t.remonttiPaattyy) {
              <div><dt>Remontti</dt>
                <dd>
                  @if (t.remonttiAlkaa) { {{ t.remonttiAlkaa | date:'dd.MM.yyyy' }} } @else { ? }
                  –
                  @if (t.remonttiPaattyy) { {{ t.remonttiPaattyy | date:'dd.MM.yyyy' }} } @else { ? }
                </dd>
              </div>
            }
            @if (t.remonttityyppi) {
              <div><dt>Remonttityyppi</dt><dd>{{ t.remonttityyppi }}</dd></div>
            }
            @if (t.tarkastusTila) {
              <div><dt>Tarkastuksen tila</dt><dd>{{ t.tarkastusTila }}</dd></div>
            }
            <div><dt>Aluetoimisto</dt><dd>{{ t.aluetoimisto || '—' }}</dd></div>
            <div><dt>Markkinointialue</dt><dd>{{ t.markkinointialue || '—' }}</dd></div>
            @if (t.isannoitsija) {
              <div><dt>Isännöitsijä</dt><dd>{{ t.isannoitsija }}</dd></div>
            }
            @if (t.markkinoija) {
              <div><dt>Markkinoija</dt><dd>{{ t.markkinoija }}</dd></div>
            }
            @if (t.prio) {
              <div><dt>Prio</dt><dd>{{ t.prio }}</dd></div>
            }
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
          @if (t.brochureUrl) {
            <a class="external" [href]="t.brochureUrl" target="_blank" rel="noopener">Avaa esite ↗</a>
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

        @if (t.muistio || t.huoneistoMuistio || t.kuvaus || t.lisaTieto) {
          <section>
            <h2>Muistiot</h2>
            @if (t.muistio) {
              <p class="text-block"><strong>Tiskilistan muistio:</strong> {{ t.muistio }}</p>
            }
            @if (t.huoneistoMuistio) {
              <p class="text-block"><strong>Huoneiston muistio:</strong> {{ t.huoneistoMuistio }}</p>
            }
            @if (t.kuvaus) { <p class="text-block">{{ t.kuvaus }}</p> }
            @if (t.lisaTieto) { <p class="text-block">{{ t.lisaTieto }}</p> }
          </section>
        }

        <section class="action-sheet" aria-label="Toiminnot">
          <h2>Toiminnot</h2>
          <ul class="action-list">
            <li>
              <button
                type="button"
                class="action action--primary"
                (click)="pikavaraus(t)"
              >
                <span class="action-label">Pikavaraus</span>
                <span class="action-hint">Tee varaus tästä huoneistosta</span>
              </button>
            </li>
            <li>
              <button
                type="button"
                class="action"
                (click)="lisaaRemontti(t)"
              >
                <span class="action-label">Lisää remontti</span>
                <span class="action-hint">Kirjaa remontin alkamis- ja päättymispäivä</span>
              </button>
            </li>
            <li>
              <button
                type="button"
                class="action"
                (click)="laskeTiskilista(t)"
              >
                <span class="action-label">Laske tiskilista</span>
                <span class="action-hint">Päivitä tiskilistan laskenta</span>
              </button>
            </li>
            @if (t.lumoUrl) {
              <li>
                <a
                  class="action action--link"
                  [href]="t.lumoUrl"
                  target="_blank"
                  rel="noopener"
                >
                  <span class="action-label">Avaa Lumo Verkossa ↗</span>
                </a>
              </li>
            }
            @if (t.brochureUrl) {
              <li>
                <a
                  class="action action--link"
                  [href]="t.brochureUrl"
                  target="_blank"
                  rel="noopener"
                >
                  <span class="action-label">Avaa esite ↗</span>
                </a>
              </li>
            }
          </ul>
        </section>
        }
      }
    </main>

    <dx-toast
      [visible]="toast().visible"
      [message]="toast().message"
      [type]="toast().type"
      [displayTime]="2500"
      (onHiding)="onToastHide()"
    ></dx-toast>
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

  // -- Action sheet ----------------------------------------------------------

  protected readonly toast = signal<ToastState>(TOAST_HIDDEN);

  protected pikavaraus(_t: TiskilistaDetailDto): void {
    // XAF action TiskilistaViewController.PikaVarausAction. Backend wiring
    // is a follow-up — opens a quick-reservation form inside the PWA.
    this.flash('Pikavaraus — toiminnallisuus tulossa', 'info');
  }

  protected lisaaRemontti(_t: TiskilistaDetailDto): void {
    // XAF action TiskilistaViewController.LisaaRemonttiAction.
    this.flash('Lisää remontti — toiminnallisuus tulossa', 'info');
  }

  protected laskeTiskilista(_t: TiskilistaDetailDto): void {
    // XAF action TiskilistaViewController.LaskeTiskilistaAction. Triggers a
    // server-side recalculation of the underlying view; until that endpoint
    // exists we just acknowledge the click.
    this.flash('Laske tiskilista — toiminnallisuus tulossa', 'info');
  }

  protected onToastHide(): void {
    this.toast.set(TOAST_HIDDEN);
  }

  private flash(message: string, type: ToastType): void {
    this.toast.set({ visible: true, message, type });
  }
}
