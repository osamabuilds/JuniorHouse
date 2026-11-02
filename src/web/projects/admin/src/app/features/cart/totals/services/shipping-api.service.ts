import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiClient } from '@core/http';
import { Page } from '@shared';
import { CityDto, ProvinceDto, SettingsValue, ShippingRateDto, ShippingSettingsDto } from '../models';

/** HTTP only, no state (CONVENTIONS section 1). The routes are listed in docs/specs/cart/plan.md. */
@Injectable({ providedIn: 'root' })
export class ShippingApiService {
  private readonly api = inject(ApiClient);

  rates(page: number, pageSize: number): Observable<Page<ShippingRateDto>> {
    return this.api.get<Page<ShippingRateDto>>('/api/shipping/rates', { page, pageSize });
  }

  setRate(cityId: number, fee: number): Observable<ShippingRateDto> {
    return this.api.put<ShippingRateDto>(`/api/shipping/rates/${cityId}`, { fee });
  }

  removeRate(cityId: number): Observable<void> {
    return this.api.delete<void>(`/api/shipping/rates/${cityId}`);
  }

  settings(): Observable<ShippingSettingsDto> {
    return this.api.get<ShippingSettingsDto>('/api/shipping/settings');
  }

  saveSettings(value: SettingsValue): Observable<ShippingSettingsDto> {
    return this.api.put<ShippingSettingsDto>('/api/shipping/settings', value);
  }

  provinces(): Observable<readonly ProvinceDto[]> {
    return this.api.get<readonly ProvinceDto[]>('/api/geography/provinces');
  }

  cities(provinceId: number): Observable<readonly CityDto[]> {
    return this.api.get<readonly CityDto[]>(`/api/geography/provinces/${provinceId}/cities`);
  }
}
