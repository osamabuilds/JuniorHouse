import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { SiteSettings } from '../../../../shared/models/site-settings.model';
import { SETTINGS_API_URL } from '../site-settings.providers';

/** HTTP only: fetches the published settings document (SCRUM-206). */
@Injectable({ providedIn: 'root' })
export class SiteSettingsApi {
  private readonly http = inject(HttpClient);
  private readonly url = inject(SETTINGS_API_URL);

  get(): Observable<SiteSettings> {
    return this.http.get<SiteSettings>(this.url ?? '/api/storefront/settings');
  }
}
