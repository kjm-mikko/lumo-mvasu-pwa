import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';

import { environment } from '../../../environments/environment';
import type {
  TaskAccent,
  TaskAction,
  TaskActionKind,
  TaskCard,
  TaskGroup,
  TaskGroupId,
  TaskType,
} from '../models/task.dto';
import type {
  TaskCustomer,
  TaskDetail,
  TaskNote,
  TaskRowAction,
  TaskRowActionKind,
} from '../models/task-detail.dto';

/** Wire shape coming back from `GET /api/tasks`. Mirrors mVasu.Api.Contracts.TasksResponseDto. */
interface TasksResponseDto {
  readonly groups: ReadonlyArray<TaskGroupDto>;
  readonly total: number;
  readonly generatedAt: string;
}

interface TaskGroupDto {
  readonly id: string;
  readonly label: string;
  readonly date: string;
  readonly tasks: ReadonlyArray<TaskDto>;
}

interface TaskDto {
  readonly id: string;
  readonly type: string;
  readonly accent: string;
  readonly urgent: boolean;
  readonly when: { readonly time: string; readonly note?: string | null; readonly sortOrder: number };
  readonly title: string;
  readonly who?: string | null;
  readonly meta?: string | null;
  readonly typeLabel?: string | null;
  readonly actions: ReadonlyArray<TaskActionDto>;
  readonly entityRef: { readonly module: string; readonly entityType: string; readonly id: string };
  readonly sortOrder?: number | null;
}

interface TaskActionDto {
  readonly kind: string;
  readonly label: string;
  readonly primary: boolean;
  readonly destructive: boolean;
  readonly href?: string | null;
}

/** Wire shape for `GET /api/tasks/:id`. Mirrors mVasu.Api.Contracts.TaskDetailDto. */
interface TaskDetailWireDto {
  readonly id: string;
  readonly type: string;
  readonly typeLabel: string;
  readonly accent: string;
  readonly timeContext: string;
  readonly title: string;
  readonly subtitle?: string | null;
  readonly customer?: TaskCustomerWireDto | null;
  readonly actions: ReadonlyArray<TaskRowActionWireDto>;
  readonly note?: TaskNoteWireDto | null;
  readonly primaryCta: TaskActionDto;
  readonly entityRef: { readonly module: string; readonly entityType: string; readonly id: string };
}

interface TaskCustomerWireDto {
  readonly id: string;
  readonly name: string;
  readonly initials: string;
  readonly phone?: string | null;
  readonly email?: string | null;
}

interface TaskRowActionWireDto {
  readonly id: string;
  readonly label: string;
  readonly kind: string;
  readonly destructive: boolean;
  readonly confirm?: { readonly title: string; readonly body: string } | null;
  readonly href?: string | null;
}

interface TaskNoteWireDto {
  readonly id: string;
  readonly body: string;
  readonly updatedAt: string;
}

export interface TasksQueryOptions {
  readonly from?: string;          // ISO date YYYY-MM-DD
  readonly to?: string;
  readonly userId?: string;        // 'me' default at the API
  readonly types?: ReadonlyArray<TaskType>;
  readonly urgentOnly?: boolean;
}

@Injectable({ providedIn: 'root' })
export class TasksApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiBaseUrl;

  /**
   * GET /api/tasks. Maps the wire DTO to the frontend's TaskGroup +
   * TaskCard shapes — the shapes intentionally diverge in two places:
   *
   *  1. The wire has explicit `primary` / `destructive` booleans on
   *     actions; the frontend keeps them as optional flags.
   *  2. The wire's TaskWhen.sortOrder is a unix-millis number used for
   *     server-side sorting; the frontend keeps `sortOrder` on the
   *     TaskCard itself for in-memory ordering.
   */
  list(options: TasksQueryOptions = {}): Observable<ReadonlyArray<TaskGroup>> {
    let params = new HttpParams();
    if (options.from)        params = params.set('from', options.from);
    if (options.to)          params = params.set('to', options.to);
    if (options.userId)      params = params.set('userId', options.userId);
    if (options.urgentOnly)  params = params.set('urgentOnly', 'true');
    if (options.types && options.types.length > 0) {
      params = params.set('types', options.types.join(','));
    }

    return this.http
      .get<TasksResponseDto>(`${this.baseUrl}/api/tasks`, { params })
      .pipe(map(response => response.groups.map(toTaskGroup)));
  }

  get(id: string): Observable<TaskDetail> {
    return this.http
      .get<TaskDetailWireDto>(`${this.baseUrl}/api/tasks/${id}`)
      .pipe(map(toTaskDetail));
  }
}

function toTaskGroup(g: TaskGroupDto): TaskGroup {
  return {
    id: g.id as TaskGroupId,
    label: g.label,
    date: g.date,
    tasks: g.tasks.map(toTaskCard),
  };
}

function toTaskCard(t: TaskDto): TaskCard {
  return {
    id: t.id,
    type: t.type as TaskType,
    accent: t.accent as TaskAccent,
    urgent: t.urgent,
    when: { time: t.when.time, note: t.when.note ?? undefined },
    title: t.title,
    who: t.who ?? undefined,
    meta: t.meta ?? undefined,
    typeLabel: t.typeLabel ?? undefined,
    actions: t.actions.map(toTaskAction),
    entityRef: { module: t.entityRef.module, id: t.entityRef.id },
    sortOrder: t.sortOrder ?? undefined,
  };
}

function toTaskAction(a: TaskActionDto): TaskAction {
  return {
    kind: a.kind as TaskActionKind,
    label: a.label,
    primary: a.primary || undefined,
    destructive: a.destructive || undefined,
    href: a.href ?? undefined,
  };
}

function toTaskDetail(d: TaskDetailWireDto): TaskDetail {
  return {
    id: d.id,
    type: d.type as TaskType,
    typeLabel: d.typeLabel,
    accent: d.accent as TaskAccent,
    timeContext: d.timeContext,
    title: d.title,
    subtitle: d.subtitle ?? undefined,
    customer: d.customer ? toTaskCustomer(d.customer) : undefined,
    actions: d.actions.map(toTaskRowAction),
    note: d.note ? toTaskNote(d.note) : null,
    primaryCta: toTaskAction(d.primaryCta),
    entityRef: { module: d.entityRef.module, id: d.entityRef.id },
  };
}

function toTaskCustomer(c: TaskCustomerWireDto): TaskCustomer {
  return {
    id: c.id,
    name: c.name,
    initials: c.initials,
    phone: c.phone ?? undefined,
    email: c.email ?? undefined,
  };
}

function toTaskRowAction(a: TaskRowActionWireDto): TaskRowAction {
  return {
    id: a.id,
    label: a.label,
    kind: a.kind as TaskRowActionKind,
    destructive: a.destructive || undefined,
    confirm: a.confirm ?? undefined,
    href: a.href ?? undefined,
  };
}

function toTaskNote(n: TaskNoteWireDto): TaskNote {
  return {
    id: n.id,
    body: n.body,
    updatedAt: n.updatedAt,
  };
}
