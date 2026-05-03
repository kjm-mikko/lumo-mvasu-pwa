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
