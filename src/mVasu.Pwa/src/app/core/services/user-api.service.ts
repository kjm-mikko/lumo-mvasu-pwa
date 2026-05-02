import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { environment } from '../../../environments/environment';
import type { UserProfileDto } from '../models/user-profile.dto';
import type { UpdateSettingsDto } from '../models/update-settings.dto';
import type { UpdateLocationConsentDto } from '../models/update-location-consent.dto';
import type { UserLocationDto } from '../models/user-location.dto';

@Injectable({ providedIn: 'root' })
export class UserApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiBaseUrl;

  getProfile(): Observable<UserProfileDto> {
    return this.http.get<UserProfileDto>(`${this.baseUrl}/api/me`);
  }

  updateSettings(dto: UpdateSettingsDto): Observable<UserProfileDto> {
    return this.http.put<UserProfileDto>(`${this.baseUrl}/api/me/settings`, dto);
  }

  updateLocationConsent(consent: boolean): Observable<UserProfileDto> {
    const body: UpdateLocationConsentDto = { consent };
    return this.http.put<UserProfileDto>(`${this.baseUrl}/api/me/location-consent`, body);
  }

  recordLocation(location: UserLocationDto): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/api/me/location`, location);
  }
}
