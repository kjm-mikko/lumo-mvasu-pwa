import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
} from '@angular/core';
import { Router } from '@angular/router';
import { DxListModule } from 'devextreme-angular/ui/list';
import { DxLoadPanelModule } from 'devextreme-angular/ui/load-panel';
import { DxToastModule } from 'devextreme-angular/ui/toast';

import type { TaskAction, TaskCard, TaskGroup } from '../../core/models/task.dto';
import { buildMockTaskGroups } from './mock-tasks';
import { TaskCardComponent } from './task-card/task-card.component';

interface DxTaskGroup {
  readonly key: string;
  readonly label: string;
  readonly date: string;
  readonly count: number;
  readonly items: ReadonlyArray<TaskCard>;
}

type ToastType = 'info' | 'success' | 'warning' | 'error';

interface ToastState {
  readonly visible: boolean;
  readonly message: string;
  readonly type: ToastType;
}

const TOAST_HIDDEN: ToastState = { visible: false, message: '', type: 'info' };

/**
 * Tasks tab — first screen of the A+C navigation pattern.
 *
 * Backend integration is intentionally deferred. The component reads from
 * a mock fixture; once the BACKEND.md endpoint exists, swap the fixture
 * for an injected service that returns the same TaskGroup shape.
 */
@Component({
  selector: 'app-tasks-tab',
  imports: [DxListModule, DxLoadPanelModule, DxToastModule, TaskCardComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="topbar">
      <span class="eyebrow">Päivän työ</span>
      <h1 class="title">Tehtävät</h1>
    </section>

    @if (totalTasks() === 0 && !loading()) {
      <div class="empty">
        <span class="empty-illustration" aria-hidden="true">✓</span>
        <p class="empty-title">Päivän tehtävät on hoidettu.</p>
        <p class="empty-sub">Käy ihmettelemässä huomista listaa myöhemmin.</p>
      </div>
    } @else {
      <dx-list
        class="lumo-task-list"
        [dataSource]="dxGroups()"
        [grouped]="true"
        [collapsibleGroups]="false"
        [pullRefreshEnabled]="true"
        keyExpr="id"
        groupTemplate="group"
        itemTemplate="item"
        (onPullRefresh)="onPullRefresh()"
        (onItemClick)="onCardClick($event)"
      >
        <div *dxTemplate="let group of 'group'" class="lumo-day-header">
          <span class="day">{{ group.label }}</span>
          <span class="date">{{ group.date }}</span>
          <span class="count" [attr.aria-label]="group.count + ' tehtävää'">
            {{ group.count }} tehtävää
          </span>
        </div>

        <div *dxTemplate="let task of 'item'" class="lumo-task-list__item">
          <app-task-card
            [task]="task"
            (action)="onAction($event, task.id)"
            (kebab)="onKebab($event)"
          />
        </div>
      </dx-list>
    }

    <dx-load-panel
      [visible]="loading()"
      [shadingColor]="'rgba(255,255,255,0.6)'"
      [showIndicator]="true"
      [showPane]="true"
      [hideOnOutsideClick]="false"
      message="Ladataan tehtäviä…"
    ></dx-load-panel>

    <dx-toast
      [visible]="toast().visible"
      [message]="toast().message"
      [type]="toast().type"
      [displayTime]="2400"
      (onHiding)="onToastHide()"
    ></dx-toast>
  `,
  styleUrl: './tasks-tab.component.scss',
})
export class TasksTabComponent {
  private readonly router = inject(Router);

  protected readonly groups = signal<ReadonlyArray<TaskGroup>>(buildMockTaskGroups());
  protected readonly loading = signal<boolean>(false);
  protected readonly toast = signal<ToastState>(TOAST_HIDDEN);

  protected readonly totalTasks = computed(() =>
    this.groups().reduce((sum, g) => sum + g.tasks.length, 0),
  );

  /** dx-list expects { key, items } — flatten our TaskGroup with the extra labels. */
  protected readonly dxGroups = computed<ReadonlyArray<DxTaskGroup>>(() =>
    this.groups()
      .filter(g => g.tasks.length > 0)
      .map(g => ({
        key: g.id,
        label: g.label,
        date: g.date,
        count: g.tasks.length,
        items: g.tasks,
      })),
  );

  protected onCardClick(event: { itemData?: TaskCard }): void {
    const task = event.itemData;
    if (!task) return;
    this.router.navigate(['/tasks', task.id]).catch(() => {
      // Detail route lands in a later iteration — surface a toast for now.
      this.flash(`Tarkka näkymä tulossa: ${task.title}`, 'info');
    });
  }

  protected onAction(action: TaskAction, taskId: string): void {
    const verb = action.kind === 'mark-done'
      ? 'Tehtävä merkitty tehdyksi'
      : action.kind === 'cancel'
        ? 'Tehtävä peruutettu'
        : `Toiminto: ${action.label}`;
    this.flash(`${verb} (mock, taskId=${taskId})`, action.destructive ? 'warning' : 'success');
  }

  protected onKebab(event: { id: string; task: TaskCard }): void {
    const verbs: Record<string, string> = {
      open:     'Avataan tarkka näkymä',
      postpone: 'Tehtävä siirretty huomiselle',
      done:     'Merkitty tehdyksi',
      cancel:   'Tehtävä peruutettu',
    };
    const message = verbs[event.id] ?? `Toiminto: ${event.id}`;
    this.flash(`${message} (mock)`, event.id === 'cancel' ? 'warning' : 'info');
  }

  protected onPullRefresh(): void {
    this.loading.set(true);
    setTimeout(() => {
      this.groups.set(buildMockTaskGroups());
      this.loading.set(false);
      this.flash('Päivitetty', 'success');
    }, 600);
  }

  protected onToastHide(): void {
    this.toast.set(TOAST_HIDDEN);
  }

  private flash(message: string, type: ToastType): void {
    this.toast.set({ visible: true, message, type });
  }
}
