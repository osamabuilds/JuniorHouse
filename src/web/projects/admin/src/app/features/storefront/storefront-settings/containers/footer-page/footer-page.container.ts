import { ChangeDetectionStrategy, Component, computed, inject, linkedSignal } from '@angular/core';
import { Store } from '@ngrx/store';
import { SectionNavComponent } from '@shared';
import { FooterEditorComponent } from '../../components/footer-editor/footer-editor.component';
import { SaveBarComponent } from '../../components/save-bar/save-bar.component';
import { SettingsBannersComponent } from '../../components/settings-banners/settings-banners.component';
import { FooterColumn } from '../../models';
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
import { withSort } from '../../utils/list.util';
import { HasUnsavedChanges } from '../../utils/unsaved-changes.guard';
import { HasPermissionDirective } from '@core/auth';

/** Footer (S-3, ST-1): columns and links, shown on every page of the shop once published. */
@Component({
  selector: 'app-footer-page',
  imports: [HasPermissionDirective, SectionNavComponent, FooterEditorComponent, SaveBarComponent, SettingsBannersComponent],
  templateUrl: './footer-page.container.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class FooterPageContainer implements HasUnsavedChanges {
  private readonly store = inject(Store);

  readonly links = STOREFRONT_LINKS;
  readonly draft = this.store.selectSignal(selectDraft);
  readonly loading = this.store.selectSignal(selectLoading);
  readonly loadError = this.store.selectSignal(selectLoadError);
  readonly saving = this.store.selectSignal(selectSaving);
  readonly saveError = this.store.selectSignal(selectSaveError);
  readonly notice = this.store.selectSignal(selectNotice);

  readonly columns = linkedSignal<readonly FooterColumn[]>(() => this.draft()?.footer ?? []);
  readonly dirty = computed(() => JSON.stringify(this.columns()) !== JSON.stringify(this.draft()?.footer ?? []));
  readonly fieldErrors = computed(() => this.saveError()?.fieldErrors ?? {});

  constructor() {
    this.store.dispatch(StorefrontSettingsPageActions.draftOpened());
  }

  hasUnsavedChanges(): boolean {
    return this.dirty();
  }

  addColumn(): void {
    this.columns.update((columns) => withSort([...columns, { title: '', sort: columns.length + 1, links: [] }]));
  }

  discard(): void {
    this.columns.set(this.draft()?.footer ?? []);
  }

  save(): void {
    this.store.dispatch(StorefrontSettingsPageActions.saveSubmitted({ part: { part: 'footer', columns: withSort(this.columns()) } }));
  }

  reload(): void {
    this.store.dispatch(StorefrontSettingsPageActions.draftOpened());
  }

  dismissNotice(): void {
    this.store.dispatch(StorefrontSettingsPageActions.noticeDismissed());
  }
}
