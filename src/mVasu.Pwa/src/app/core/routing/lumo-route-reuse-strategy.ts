import { ActivatedRouteSnapshot, DetachedRouteHandle, RouteReuseStrategy } from '@angular/router';

/**
 * Reuses route components when a route's data has reuse=true.
 *
 * The Tiskilista list opts in via `data: { reuse: true }`, so navigating to
 * a detail and back restores its scroll position, search input, filters and
 * loaded page without re-running the API request.
 */
export class LumoRouteReuseStrategy implements RouteReuseStrategy {
  private readonly handles = new Map<string, DetachedRouteHandle>();

  shouldDetach(route: ActivatedRouteSnapshot): boolean {
    return route.data?.['reuse'] === true;
  }

  store(route: ActivatedRouteSnapshot, handle: DetachedRouteHandle | null): void {
    const key = this.keyFor(route);
    if (!key) return;
    if (handle === null) {
      this.handles.delete(key);
    } else {
      this.handles.set(key, handle);
    }
  }

  shouldAttach(route: ActivatedRouteSnapshot): boolean {
    if (route.data?.['reuse'] !== true) return false;
    const key = this.keyFor(route);
    return key !== null && this.handles.has(key);
  }

  retrieve(route: ActivatedRouteSnapshot): DetachedRouteHandle | null {
    if (route.data?.['reuse'] !== true) return null;
    const key = this.keyFor(route);
    return key === null ? null : this.handles.get(key) ?? null;
  }

  shouldReuseRoute(future: ActivatedRouteSnapshot, curr: ActivatedRouteSnapshot): boolean {
    return future.routeConfig === curr.routeConfig;
  }

  private keyFor(route: ActivatedRouteSnapshot): string | null {
    return route.routeConfig?.path ?? null;
  }
}
