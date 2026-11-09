import { ChangeDetectionStrategy, Component, computed, inject, linkedSignal } from '@angular/core';
import { Store } from '@ngrx/store';
import { SectionNavComponent } from '@shared';
import { DeliveryEditorComponent } from '../../components/delivery-editor/delivery-editor.component';
import { SaveBarComponent } from '../../components/save-bar/save-bar.component';
import { SettingsBannersComponent } from '../../components/settings-banners/settings-banners.component';
import { STOREFRONT_LINKS } from '../../storefront-settings.constants';
import {
  StorefrontSettingsPageActions,
  selectDraft,
  selectLoadError,
  selectLoading,
  selectNotice,
  selectSaveError,
  selectSaving,
} from '../../store';
import { DeliveryValues, blankRule, toDeliveryValues, toExchange } from '../../utils/delivery.util';
import { HasUnsavedChanges } from '../../utils/unsaved-changes.guard';
import { HasPermissionDirective } from '@core/auth';

/** Delivery and exchange (S-3, ST-7): the city table and the exchange policy that the delivery and exchange pages quote. */
@Component({
  selector: 'app-delivery-page',
  imports: [HasPermissionDirective, SectionNavComponent, DeliveryEditorComponent, SaveBarComponent, SettingsBannersComponent],
  templateUrl: './delivery-page.container.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DeliveryPageContainer implements HasUnsavedChanges {
  private readonly store = inject(Store);

  readonly links = STOREFRONT_LINKS;
  readonly draft = this.store.selectSignal(selectDraft);
  readonly loading = this.store.selectSignal(selectLoading);
  readonly loadError = this.store.selectSignal(selectLoadError);
  readonly saving = this.store.selectSignal(selectSaving);
  readonly saveError = this.store.selectSignal(selectSaveError);
  readonly notice = this.store.selectSignal(selectNotice);

  readonly values = linkedSignal<DeliveryValues>(() => toDeliveryValues(this.draft()));
  readonly dirty = computed(() => JSON.stringify(this.values()) !== JSON.stringify(toDeliveryValues(this.draft())));
  readonly fieldErrors = computed(() => this.saveError()?.fieldErrors ?? {});

  constructor() {
    this.store.dispatch(StorefrontSettingsPageActions.draftOpened());
  }

  hasUnsavedChanges(): boolean {
    return this.dirty();
  }

  addRule(): void {
    this.values.update((values) => ({ ...values, rules: [...values.rules, blankRule()] }));
  }

  discard(): void {
    this.values.set(toDeliveryValues(this.draft()));
  }

  save(): void {
    const values = this.values();
    this.store.dispatch(StorefrontSettingsPageActions.saveSubmitted({ part: { part: 'delivery', rules: values.rules, exchange: toExchange(values) } }));
  }

  reload(): void {
    this.store.dispatch(StorefrontSettingsPageActions.draftOpened());
  }

  dismissNotice(): void {
    this.store.dispatch(StorefrontSettingsPageActions.noticeDismissed());
  }
}
