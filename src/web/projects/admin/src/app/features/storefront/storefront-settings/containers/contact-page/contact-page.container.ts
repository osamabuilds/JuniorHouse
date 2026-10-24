import { ChangeDetectionStrategy, Component, computed, inject, linkedSignal } from '@angular/core';
import { Store } from '@ngrx/store';
import { SectionNavComponent } from '@shared';
import { ContactFormComponent } from '../../components/contact-form/contact-form.component';
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
import { ContactValues, cleanSearches, toContactValues, toSettingItems } from '../../utils/contact.util';
import { HasUnsavedChanges } from '../../utils/unsaved-changes.guard';

/** Settings (S-3, ST-4, ST-7): brand, WhatsApp, email, hours, the announcement bar and the popular searches. */
@Component({
  selector: 'app-contact-page',
  imports: [SectionNavComponent, ContactFormComponent, SaveBarComponent, SettingsBannersComponent],
  templateUrl: './contact-page.container.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ContactPageContainer implements HasUnsavedChanges {
  private readonly store = inject(Store);

  readonly links = STOREFRONT_LINKS;
  readonly draft = this.store.selectSignal(selectDraft);
  readonly loading = this.store.selectSignal(selectLoading);
  readonly loadError = this.store.selectSignal(selectLoadError);
  readonly saving = this.store.selectSignal(selectSaving);
  readonly saveError = this.store.selectSignal(selectSaveError);
  readonly notice = this.store.selectSignal(selectNotice);

  readonly values = linkedSignal<ContactValues>(() => toContactValues(this.draft()));
  readonly dirty = computed(() => JSON.stringify(this.values()) !== JSON.stringify(toContactValues(this.draft())));
  readonly fieldErrors = computed(() => this.saveError()?.fieldErrors ?? {});

  constructor() {
    this.store.dispatch(StorefrontSettingsPageActions.draftOpened());
  }

  hasUnsavedChanges(): boolean {
    return this.dirty();
  }

  discard(): void {
    this.values.set(toContactValues(this.draft()));
  }

  save(): void {
    const values = this.values();
    this.store.dispatch(
      StorefrontSettingsPageActions.saveSubmitted({
        part: { part: 'contact', items: toSettingItems(values), popularSearches: cleanSearches(values.popularSearches) },
      }),
    );
  }

  reload(): void {
    this.store.dispatch(StorefrontSettingsPageActions.draftOpened());
  }

  dismissNotice(): void {
    this.store.dispatch(StorefrontSettingsPageActions.noticeDismissed());
  }
}
