import { ChangeDetectionStrategy, Component, effect, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Store } from '@ngrx/store';
import { HasPermissionDirective } from '@core/auth';
import { PaginationComponent, inputValue } from '@shared';
import { ShippingRateDto } from '../../models';
import {
  ShippingPageActions,
  selectCities,
  selectError,
  selectLoading,
  selectNotice,
  selectPage,
  selectPageSize,
  selectProvinces,
  selectRateSaving,
  selectRates,
  selectRemovingCityId,
  selectSettings,
  selectSettingsSaving,
  selectTotal,
} from '../../store';

/**
 * Delivery fees (docs/specs/cart/totals, TOT-5, Q-TOT-1): the shop-wide settings (default fee, free-delivery amount, Cash on Delivery fee)
 * and a fee per city. Staff with the shipping permission set them here; a change applies from the next basket, and placed orders keep the
 * fee they were placed with. The server checks every value again.
 */
@Component({
  selector: 'app-shipping-page',
  imports: [ReactiveFormsModule, HasPermissionDirective, PaginationComponent],
  templateUrl: './shipping-page.container.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ShippingPageContainer {
  private readonly store = inject(Store);
  private readonly fb = inject(FormBuilder);

  readonly settings = this.store.selectSignal(selectSettings);
  readonly settingsSaving = this.store.selectSignal(selectSettingsSaving);
  readonly rates = this.store.selectSignal(selectRates);
  readonly total = this.store.selectSignal(selectTotal);
  readonly currentPage = this.store.selectSignal(selectPage);
  readonly pageSize = this.store.selectSignal(selectPageSize);
  readonly loading = this.store.selectSignal(selectLoading);
  readonly error = this.store.selectSignal(selectError);
  readonly notice = this.store.selectSignal(selectNotice);
  readonly provinces = this.store.selectSignal(selectProvinces);
  readonly cities = this.store.selectSignal(selectCities);
  readonly rateSaving = this.store.selectSignal(selectRateSaving);
  readonly removingCityId = this.store.selectSignal(selectRemovingCityId);

  readonly settingsForm = this.fb.group({
    defaultFee: this.fb.control<number | null>(null, [Validators.required, Validators.min(0), Validators.max(100000)]),
    freeThreshold: this.fb.control<number | null>(null, [Validators.min(1)]),
    codFee: this.fb.control<number | null>(null, [Validators.min(0), Validators.max(100000)]),
  });

  readonly rateForm = this.fb.group({
    provinceId: this.fb.control<number | null>(null, [Validators.required]),
    cityId: this.fb.control<number | null>(null, [Validators.required]),
    fee: this.fb.control<number | null>(null, [Validators.required, Validators.min(0), Validators.max(100000)]),
  });

  constructor() {
    this.store.dispatch(ShippingPageActions.opened());

    // The form shows what is saved, and follows a save.
    effect(() => {
      const s = this.settings();
      if (s) this.settingsForm.reset({ defaultFee: s.defaultFee, freeThreshold: s.freeThreshold, codFee: s.codFee });
    });
  }

  saveSettings(): void {
    this.settingsForm.markAllAsTouched();
    if (this.settingsForm.invalid) return;
    const v = this.settingsForm.getRawValue();
    this.store.dispatch(ShippingPageActions.settingsSubmitted({ value: { defaultFee: v.defaultFee ?? 0, freeThreshold: v.freeThreshold ?? null, codFee: v.codFee ?? null } }));
  }

  chooseProvince(event: Event): void {
    const id = Number(inputValue(event));
    this.rateForm.controls.cityId.reset(null);
    if (id > 0) this.store.dispatch(ShippingPageActions.provinceChosen({ provinceId: id }));
  }

  addRate(): void {
    this.rateForm.markAllAsTouched();
    if (this.rateForm.invalid) return;
    const { cityId, fee } = this.rateForm.getRawValue();
    this.store.dispatch(ShippingPageActions.rateSubmitted({ cityId: cityId ?? 0, fee: fee ?? 0 }));
    this.rateForm.reset();
  }

  remove(rate: ShippingRateDto): void {
    this.store.dispatch(ShippingPageActions.rateRemoved({ cityId: rate.cityId }));
  }

  dismissNotice(): void {
    this.store.dispatch(ShippingPageActions.noticeDismissed());
  }

  changePage(page: number): void {
    this.store.dispatch(ShippingPageActions.pageChanged({ page }));
  }

  changePageSize(pageSize: number): void {
    this.store.dispatch(ShippingPageActions.pageSizeChanged({ pageSize }));
  }
}
