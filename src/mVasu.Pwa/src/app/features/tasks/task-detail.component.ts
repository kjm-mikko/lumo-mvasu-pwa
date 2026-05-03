import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  linkedSignal,
  signal,
} from '@angular/core';
import { Location } from '@angular/common';
import { DxButtonModule } from 'devextreme-angular/ui/button';
import { DxListModule } from 'devextreme-angular/ui/list';
import { DxPopupModule } from 'devextreme-angular/ui/popup';
import { DxTextAreaModule } from 'devextreme-angular/ui/text-area';
import { DxToastModule } from 'devextreme-angular/ui/toast';

import { buildMockTaskDetail } from './mock-tasks';
import type {
  TaskCustomer,
  TaskDetail,
  TaskRowAction,
} from '../../core/models/task-detail.dto';

type ToastType = 'info' | 'success' | 'warning' | 'error';

interface ToastState {
  readonly visible: boolean;
  readonly message: string;
  readonly type: ToastType;
}

const TOAST_HIDDEN: ToastState = { visible: false, message: '', type: 'info' };

interface ConfirmState {
  readonly visible: boolean;
  readonly title: string;
  readonly body: string;
  readonly action: TaskRowAction | null;
}

const CONFIRM_HIDDEN: ConfirmState = { visible: false, title: '', body: '', action: null };

interface ReasonOption {
  readonly id: string;
  readonly label: string;
}

const REASON_OPTIONS: ReadonlyArray<ReasonOption> = [
  { id: 'customer-cancelled',  label: 'Asiakas peruutti' },
  { id: 'customer-no-show',    label: 'Asiakas ei saapunut' },
  { id: 'unsuitable',          label: 'Kohde ei sovellu' },
  { id: 'reschedule',          label: 'Sovittiin uusi aika' },
  { id: 'technical',           label: 'Tekninen ongelma' },
  { id: 'other',               label: 'Muu syy' },
];

interface ReasonState {
  readonly visible: boolean;
  readonly action: TaskRowAction | null;
  readonly selectedId: string | null;
  readonly comment: string;
}

const REASON_HIDDEN: ReasonState = { visible: false, action: null, selectedId: null, comment: '' };

/**
 * Detail view for a single task — Tutustumiskäynti / Yleisesittely /
 * Tarjous / Tehtävä etc. Mock-data only; the BACKEND.md `/api/tasks/:id`
 * endpoint will replace `buildMockTaskDetail()` later.
 *
 * Layout follows SCREENS.md §04: hero block, ASIAKAS, TOIMINNOT,
 * MUISTIINPANOT, footer CTA. Confirmation dialogs use dx-popup; row
 * actions are a dx-list itemTemplate; the note is a dx-text-area.
 */
@Component({
  selector: 'app-task-detail',
  imports: [
    DxButtonModule,
    DxListModule,
    DxPopupModule,
    DxTextAreaModule,
    DxToastModule,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="topbar">
      <dx-button
        class="back"
        icon="back"
        stylingMode="text"
        [elementAttr]="{ 'aria-label': 'Takaisin' }"
        (onClick)="goBack()"
      ></dx-button>
      <span class="topbar-title">{{ detail()?.typeLabel ?? 'Tehtävä' }}</span>
    </section>

    @if (detail(); as t) {
      <article class="detail" [attr.data-accent]="t.accent">
        <header class="hero">
          <span class="hero-pill">
            <span class="hero-pill-icon" aria-hidden="true">⏱</span>
            {{ t.timeContext }}
          </span>
          <h1 class="hero-title">{{ t.title }}</h1>
          @if (t.subtitle) {
            <p class="hero-sub">{{ t.subtitle }}</p>
          }
        </header>

        @if (t.customer; as c) {
          <section>
            <h2 class="ds-eyebrow">Asiakas</h2>
            <dx-list
              class="lumo-contact-list"
              [dataSource]="customerItems(c)"
              keyExpr="id"
              [activeStateEnabled]="false"
              [focusStateEnabled]="false"
              itemTemplate="contact"
            >
              <div *dxTemplate="let item of 'contact'" class="contact">
                <span class="avatar">{{ item.initials }}</span>
                <div class="info">
                  <span class="name">{{ item.name }}</span>
                  <span class="meta">
                    {{ item.phone ?? '—' }}{{ item.email ? ' · ' + item.email : '' }}
                  </span>
                </div>
                @if (item.phone) {
                  <dx-button
                    class="contact-call"
                    icon="tel"
                    stylingMode="text"
                    [elementAttr]="{ 'aria-label': 'Soita asiakkaalle' }"
                    (onClick)="callCustomer(item)"
                  ></dx-button>
                }
              </div>
            </dx-list>
          </section>
        }

        <section>
          <h2 class="ds-eyebrow">Toiminnot</h2>
          <dx-list
            class="lumo-action-list"
            [dataSource]="actions()"
            keyExpr="id"
            itemTemplate="action"
            (onItemClick)="onActionClick($event)"
          >
            <div
              *dxTemplate="let item of 'action'"
              class="row-action"
              [class.row-action--danger]="item.destructive"
            >
              <span class="row-action-label">{{ item.label }}</span>
              <span class="row-action-chev" aria-hidden="true">›</span>
            </div>
          </dx-list>
        </section>

        <section>
          <h2 class="ds-eyebrow">Muistiinpanot</h2>
          <dx-text-area
            class="lumo-note"
            [value]="noteDraft()"
            [autoResizeEnabled]="true"
            [minHeight]="80"
            placeholder="Lisää muistiinpano…"
            (onValueChanged)="onNoteChange($event)"
            (onFocusOut)="onNoteSave()"
          ></dx-text-area>
        </section>
      </article>

      <footer class="footer-cta">
        <dx-button
          class="cta"
          [text]="t.primaryCta.label"
          type="default"
          stylingMode="contained"
          (onClick)="onPrimaryCta()"
        ></dx-button>
      </footer>
    } @else {
      <p class="status status--error" role="alert">
        Tehtävää ei löytynyt (id: {{ id() }}).
      </p>
    }

    <dx-popup
      [visible]="confirm().visible"
      [title]="confirm().title"
      [width]="320"
      [height]="'auto'"
      [showCloseButton]="false"
      [hideOnOutsideClick]="true"
      (onHiding)="onConfirmCancel()"
    >
      <div *dxTemplate="let _ of 'content'" class="confirm-body">
        <p>{{ confirm().body }}</p>
        <div class="confirm-actions">
          <dx-button
            text="Peruuta"
            stylingMode="outlined"
            type="default"
            (onClick)="onConfirmCancel()"
          ></dx-button>
          <dx-button
            text="Vahvista"
            stylingMode="contained"
            type="default"
            (onClick)="onConfirmAccept()"
          ></dx-button>
        </div>
      </div>
    </dx-popup>

    <dx-popup
      [visible]="reason().visible"
      title="Peruutuksen syy"
      [width]="'90%'"
      [maxWidth]="420"
      [height]="'auto'"
      [showCloseButton]="true"
      [hideOnOutsideClick]="true"
      [position]="reasonPopupPosition"
      (onHiding)="onReasonCancel()"
    >
      <div *dxTemplate="let _ of 'content'" class="reason-body">
        <p class="reason-hint">
          Valitse syy, joka kirjataan auditiin. Kommentti on valinnainen
          paitsi "Muu syy" -tapauksessa.
        </p>
        <ul class="reason-options" role="list">
          @for (opt of reasonOptions; track opt.id) {
            <li>
              <button
                type="button"
                class="reason-option"
                [class.reason-option--active]="reason().selectedId === opt.id"
                (click)="selectReason(opt.id)"
              >
                <span class="reason-radio" aria-hidden="true">
                  @if (reason().selectedId === opt.id) {
                    <i class="dx-icon dx-icon-check"></i>
                  }
                </span>
                <span class="reason-label">{{ opt.label }}</span>
              </button>
            </li>
          }
        </ul>

        <dx-text-area
          class="lumo-note reason-comment"
          [value]="reason().comment"
          placeholder="Kommentti (valinnainen)"
          [autoResizeEnabled]="true"
          [minHeight]="64"
          (onValueChanged)="onReasonComment($event)"
        ></dx-text-area>

        <div class="reason-actions">
          <dx-button
            text="Sulje"
            stylingMode="outlined"
            type="default"
            (onClick)="onReasonCancel()"
          ></dx-button>
          <dx-button
            text="Vahvista peruutus"
            stylingMode="contained"
            type="danger"
            [disabled]="!canConfirmReason()"
            (onClick)="onReasonConfirm()"
          ></dx-button>
        </div>
      </div>
    </dx-popup>

    <dx-toast
      [visible]="toast().visible"
      [message]="toast().message"
      [type]="toast().type"
      [displayTime]="2400"
      (onHiding)="onToastHide()"
    ></dx-toast>
  `,
  styleUrl: './task-detail.component.scss',
})
export class TaskDetailComponent {
  private readonly location = inject(Location);

  /** Bound from the route param via withComponentInputBinding. */
  readonly id = input.required<string>();

  protected readonly detail = computed<TaskDetail | null>(() =>
    buildMockTaskDetail(this.id()),
  );
  protected readonly actions = computed<TaskRowAction[]>(() =>
    [...(this.detail()?.actions ?? [])],
  );

  /**
   * Auto-initialises from the resolved detail's note body when the route id
   * (and therefore the detail) changes. The user can still overwrite it via
   * the dx-text-area; linkedSignal stores the override until the source
   * changes again. Avoids reading `input.required()` from the constructor,
   * which would throw NG0950 before Angular finishes the input binding.
   */
  protected readonly noteDraft = linkedSignal<string>(
    () => this.detail()?.note?.body ?? '',
  );
  protected readonly toast = signal<ToastState>(TOAST_HIDDEN);
  protected readonly confirm = signal<ConfirmState>(CONFIRM_HIDDEN);
  protected readonly reason = signal<ReasonState>(REASON_HIDDEN);

  protected readonly reasonOptions = REASON_OPTIONS;

  /** Bottom-sheet positioning per BEHAVIOR.md §7 (Bottom-sheet slide up 280 ms). */
  protected readonly reasonPopupPosition = {
    my: { x: 'center', y: 'bottom' } as const,
    at: { x: 'center', y: 'bottom' } as const,
    of: 'window',
    offset: { x: 0, y: 0 },
  };

  protected readonly canConfirmReason = computed<boolean>(() => {
    const r = this.reason();
    if (!r.selectedId) return false;
    if (r.selectedId === 'other' && r.comment.trim().length === 0) return false;
    return true;
  });

  protected customerItems(c: TaskCustomer): TaskCustomer[] {
    return [c];
  }

  protected callCustomer(c: TaskCustomer): void {
    if (!c.phone) return;
    window.location.href = `tel:${c.phone.replace(/\s+/g, '')}`;
  }

  protected onActionClick(event: { itemData?: TaskRowAction }): void {
    const action = event.itemData;
    if (!action) return;

    // Cancel actions go through the reason picker; pre-defined reasons
    // are required by BEHAVIOR.md §5 + auditability rules.
    if (action.kind === 'cancel') {
      this.reason.set({
        visible: true,
        action,
        selectedId: null,
        comment: '',
      });
      return;
    }

    if (action.confirm) {
      this.confirm.set({
        visible: true,
        title: action.confirm.title,
        body: action.confirm.body,
        action,
      });
      return;
    }

    this.runAction(action);
  }

  protected onConfirmAccept(): void {
    const action = this.confirm().action;
    this.confirm.set(CONFIRM_HIDDEN);
    if (action) this.runAction(action);
  }

  protected onConfirmCancel(): void {
    this.confirm.set(CONFIRM_HIDDEN);
  }

  protected selectReason(id: string): void {
    this.reason.update(r => ({ ...r, selectedId: id }));
  }

  protected onReasonComment(event: { value?: string | null }): void {
    const comment = event.value ?? '';
    this.reason.update(r => ({ ...r, comment }));
  }

  protected onReasonCancel(): void {
    this.reason.set(REASON_HIDDEN);
  }

  protected onReasonConfirm(): void {
    const r = this.reason();
    if (!r.action || !r.selectedId) return;
    const optionLabel = REASON_OPTIONS.find(o => o.id === r.selectedId)?.label ?? r.selectedId;
    const note = r.comment.trim();
    this.reason.set(REASON_HIDDEN);
    this.flash(
      note ? `Tehtävä peruutettu: ${optionLabel} — "${note}" (mock)`
           : `Tehtävä peruutettu: ${optionLabel} (mock)`,
      'warning',
    );
  }

  protected onNoteChange(event: { value?: string | null }): void {
    this.noteDraft.set(event.value ?? '');
  }

  protected onNoteSave(): void {
    const id = this.detail()?.note?.id;
    if (!id) return;
    this.flash('Muistiinpano tallennettu (mock)', 'success');
  }

  protected onPrimaryCta(): void {
    const cta = this.detail()?.primaryCta;
    if (!cta) return;
    this.flash(`Toiminto: ${cta.label} (mock)`, 'info');
  }

  protected goBack(): void {
    this.location.back();
  }

  protected onToastHide(): void {
    this.toast.set(TOAST_HIDDEN);
  }

  private runAction(action: TaskRowAction): void {
    if (action.kind === 'external' && action.href) {
      window.open(action.href, '_blank', 'noopener');
    }
    const verbs: Record<string, string> = {
      navigate:     'Siirrytään kohteeseen',
      external:     'Avataan ulkoinen linkki',
      'mark-done':  'Tehtävä merkitty tehdyksi',
      cancel:       'Tehtävä peruutettu',
      'create-offer': 'Avataan tarjouksen luonti',
    };
    const message = verbs[action.kind] ?? action.label;
    this.flash(`${message} (mock)`, action.destructive ? 'warning' : 'success');
  }

  private flash(message: string, type: ToastType): void {
    this.toast.set({ visible: true, message, type });
  }
}
