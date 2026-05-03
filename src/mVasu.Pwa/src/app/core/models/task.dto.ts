/**
 * Task queue DTOs — mirror the contracts described in
 * design/handoff_navigation/design_handoff_mvasu_navigation/BACKEND.md.
 *
 * Backend wiring is not yet in place; the PWA renders the Task tab from a
 * mock fixture today. When the API is ready, these types stay; only the
 * service that produces them changes.
 */

export type TaskType =
  | 'visit-introduction'
  | 'visit-reservation'
  | 'open-house'
  | 'signature-pending'
  | 'inbox-termination'
  | 'inbox-signed'
  | 'photo-scheduled'
  | 'renovation-approval'
  | 'lead-callback'
  | 'desk-list-item';

export type TaskAccent = 'navy' | 'cta' | 'info' | 'warn';

export type TaskActionKind =
  | 'navigate'
  | 'phone'
  | 'sms'
  | 'mark-done'
  | 'cancel'
  | 'external';

export interface TaskAction {
  readonly kind: TaskActionKind;
  readonly label: string;
  readonly href?: string;
  readonly destructive?: boolean;
  readonly primary?: boolean;
}

export interface TaskWhen {
  readonly time: string;
  readonly note?: string;
}

export interface TaskEntityRef {
  readonly module: string;
  readonly id: string;
}

export type TaskGroupId = 'today' | 'tomorrow' | 'this-week' | 'later';

export interface TaskCard {
  readonly id: string;
  readonly type: TaskType;
  readonly accent: TaskAccent;
  readonly urgent: boolean;
  readonly when: TaskWhen;
  readonly title: string;
  readonly who?: string;
  readonly meta?: string;
  readonly actions: ReadonlyArray<TaskAction>;
  readonly entityRef: TaskEntityRef;
  /** Optional explicit sort hint; lower comes first inside a group. */
  readonly sortOrder?: number;
  /**
   * Optional caption override for the card's type-row label. When present,
   * overrides the static TaskType label — used for `xVasuSecuritySystemUserTask`
   * cards where `UserTaskType.Name` (e.g. "Hinnoittelu", "Tarkastus") drives
   * the caption instead of the accent-bucket `type`.
   */
  readonly typeLabel?: string;
}

export interface TaskGroup {
  readonly id: TaskGroupId;
  readonly label: string;
  readonly date: string;
  readonly tasks: ReadonlyArray<TaskCard>;
}

export interface TasksState {
  readonly groups: ReadonlyArray<TaskGroup>;
  readonly loading: boolean;
  readonly refreshing: boolean;
  readonly error: string | null;
  readonly lastFetched: Date | null;
}
