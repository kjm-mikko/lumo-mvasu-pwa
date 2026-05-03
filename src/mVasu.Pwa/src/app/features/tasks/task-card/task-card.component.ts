import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { DxButtonModule } from 'devextreme-angular/ui/button';
import { DxDropDownButtonModule } from 'devextreme-angular/ui/drop-down-button';

import type {
  TaskAction,
  TaskCard,
  TaskType,
} from '../../../core/models/task.dto';

const TYPE_LABELS: Record<TaskType, string> = {
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

interface KebabItem {
  readonly id: string;
  readonly text: string;
  readonly icon: string;
  readonly destructive?: boolean;
}

const KEBAB_ITEMS: KebabItem[] = [
  { id: 'open',     text: 'Avaa tarkka näkymä', icon: 'arrowright' },
  { id: 'postpone', text: 'Siirrä huomiselle',  icon: 'event' },
  { id: 'done',     text: 'Merkitse tehdyksi',  icon: 'check' },
  { id: 'cancel',   text: 'Peruuta',            icon: 'close', destructive: true },
];

@Component({
  selector: 'app-task-card',
  imports: [DxButtonModule, DxDropDownButtonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <article
      class="lumo-task-card"
      [class.lumo-task-card--urgent]="task().urgent"
      [attr.data-accent]="task().accent"
      [attr.data-task-id]="task().id"
    >
      <div class="when">
        <span class="time">{{ task().when.time }}</span>
        @if (task().when.note; as note) {
          <span class="note">{{ note }}</span>
        }
      </div>

      <div class="body">
        <div class="type-row">
          <span class="type-label">{{ typeLabel() }}</span>
          @if (task().urgent) {
            <span class="urgent-pill">Kiireellinen</span>
          }
          <dx-drop-down-button
            class="kebab"
            icon="overflow"
            stylingMode="text"
            [showArrowIcon]="false"
            [dropDownOptions]="{ width: 240 }"
            displayExpr="text"
            keyExpr="id"
            [items]="kebabItems"
            (onItemClick)="onKebab($event)"
            [elementAttr]="{ 'aria-label': 'Lisätoiminnot' }"
          ></dx-drop-down-button>
        </div>

        <h3 class="title">{{ task().title }}</h3>

        @if (task().who; as who) {
          <p class="who">{{ who }}</p>
        }
        @if (task().meta; as meta) {
          <p class="meta">{{ meta }}</p>
        }

        @if (task().actions.length) {
          <div class="actions">
            @for (action of task().actions; track action.label) {
              <dx-button
                class="action-btn"
                [text]="action.label"
                [stylingMode]="action.primary ? 'contained' : 'outlined'"
                [type]="action.destructive ? 'danger' : 'default'"
                (onClick)="onAction($event, action)"
              ></dx-button>
            }
          </div>
        }
      </div>
    </article>
  `,
  styleUrl: './task-card.component.scss',
})
export class TaskCardComponent {
  readonly task = input.required<TaskCard>();

  readonly action = output<TaskAction>();
  readonly kebab = output<{ id: string; task: TaskCard }>();

  protected readonly typeLabel = computed(() => this.task().typeLabel ?? TYPE_LABELS[this.task().type]);
  protected readonly kebabItems: KebabItem[] = KEBAB_ITEMS;

  protected onAction(event: { event?: Event }, action: TaskAction): void {
    event.event?.stopPropagation();
    if (action.href) {
      if (action.kind === 'external') {
        window.open(action.href, '_blank', 'noopener');
      } else if (action.kind === 'phone' || action.kind === 'sms') {
        window.location.href = action.href;
      }
    }
    this.action.emit(action);
  }

  protected onKebab(event: { itemData?: KebabItem; event?: Event }): void {
    event.event?.stopPropagation();
    if (event.itemData) {
      this.kebab.emit({ id: event.itemData.id, task: this.task() });
    }
  }
}
