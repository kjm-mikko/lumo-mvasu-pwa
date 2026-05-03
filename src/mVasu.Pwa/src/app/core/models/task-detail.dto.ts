import type { TaskAccent, TaskAction, TaskEntityRef, TaskType } from './task.dto';

/**
 * Detail-view shape for a single task. Mirrors BEHAVIOR.md §5 plus
 * NAVIGATION.md §3d (canonical xVasuSecuritySystemUserTask DetailView
 * layout). The aggregated `/api/tasks/:id` endpoint will produce this
 * shape at runtime; for now it is built from the local mock fixture.
 */
export interface TaskDetail {
  readonly id: string;
  readonly type: TaskType;
  readonly typeLabel: string;
  readonly accent: TaskAccent;

  /** "Tänään klo 09:00 · 3 h kuluttua" */
  readonly timeContext: string;
  /** Hero title — Huoneisto.Osoite ?? Kohde.Nimi ?? Subject */
  readonly title: string;
  /** Hero subtitle — "2 h, 47 m² · vapaa 1.7. alkaen" */
  readonly subtitle?: string;

  readonly customer?: TaskCustomer;
  readonly actions: ReadonlyArray<TaskRowAction>;
  readonly note: TaskNote | null;
  readonly primaryCta: TaskAction;

  readonly entityRef: TaskEntityRef;
}

export interface TaskCustomer {
  readonly id: string;
  readonly name: string;
  readonly initials: string;
  readonly phone?: string;
  readonly email?: string;
}

export type TaskRowActionKind =
  | 'navigate'
  | 'external'
  | 'mark-done'
  | 'cancel'
  | 'create-offer';

export interface TaskRowAction {
  readonly id: string;
  readonly label: string;
  readonly kind: TaskRowActionKind;
  readonly destructive?: boolean;
  /** Confirmation modal copy when set; missing = action runs immediately. */
  readonly confirm?: { readonly title: string; readonly body: string };
  readonly href?: string;
}

export interface TaskNote {
  readonly id: string;
  readonly body: string;
  readonly updatedAt: string;
}
