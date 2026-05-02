import { Injectable, signal } from '@angular/core';

export type LocationPermission = 'granted' | 'denied' | 'prompt' | 'unsupported';

const POSITION_OPTIONS: PositionOptions = {
  enableHighAccuracy: true,
  timeout: 10_000,
  maximumAge: 30_000,
};

@Injectable({ providedIn: 'root' })
export class LocationService {
  readonly permissionState = signal<LocationPermission>('prompt');
  readonly currentPosition = signal<GeolocationPosition | null>(null);

  private watchId: number | null = null;

  constructor() {
    if (typeof navigator === 'undefined' || !('geolocation' in navigator)) {
      this.permissionState.set('unsupported');
      return;
    }

    // Permissions API is the only way to read the browser's permission decision
    // without showing a prompt. Some browsers (Safari < 16) do not support it
    // for geolocation; we silently leave the state as 'prompt' in that case.
    if ('permissions' in navigator) {
      navigator.permissions
        .query({ name: 'geolocation' as PermissionName })
        .then((status) => {
          this.permissionState.set(status.state as LocationPermission);
          status.addEventListener('change', () => {
            this.permissionState.set(status.state as LocationPermission);
          });
        })
        .catch(() => {
          // Permissions.query rejected — leave state as 'prompt'
        });
    }
  }

  /**
   * Reads the current position. If the browser shows a prompt this implicitly
   * requests permission as well; the permissionState signal updates from the
   * Permissions API listener (or here on direct success / failure).
   */
  getCurrent(): Promise<GeolocationPosition> {
    if (this.permissionState() === 'unsupported') {
      return Promise.reject(new Error('Geolocation is not supported in this browser'));
    }

    return new Promise((resolve, reject) => {
      navigator.geolocation.getCurrentPosition(
        (pos) => {
          this.currentPosition.set(pos);
          this.permissionState.set('granted');
          resolve(pos);
        },
        (err) => {
          if (err.code === err.PERMISSION_DENIED) {
            this.permissionState.set('denied');
          }
          reject(err);
        },
        POSITION_OPTIONS,
      );
    });
  }

  /**
   * Convenience wrapper used by the Settings toggle: triggers a permission
   * prompt if needed by attempting a one-shot getCurrentPosition. Returns
   * the resolved permission state without throwing.
   */
  async requestPermission(): Promise<LocationPermission> {
    if (this.permissionState() === 'unsupported') return 'unsupported';
    try {
      await this.getCurrent();
      return 'granted';
    } catch (err) {
      const code = (err as GeolocationPositionError | undefined)?.code;
      if (code === 1) return 'denied';
      // POSITION_UNAVAILABLE (2) or TIMEOUT (3) — keep current state
      return this.permissionState();
    }
  }

  watchPosition(): void {
    if (this.watchId !== null) return;
    if (this.permissionState() === 'unsupported') return;

    this.watchId = navigator.geolocation.watchPosition(
      (pos) => {
        this.currentPosition.set(pos);
        this.permissionState.set('granted');
      },
      (err) => {
        if (err.code === err.PERMISSION_DENIED) {
          this.permissionState.set('denied');
          this.stopWatch();
        }
      },
      POSITION_OPTIONS,
    );
  }

  stopWatch(): void {
    if (this.watchId !== null) {
      navigator.geolocation.clearWatch(this.watchId);
      this.watchId = null;
    }
  }
}
