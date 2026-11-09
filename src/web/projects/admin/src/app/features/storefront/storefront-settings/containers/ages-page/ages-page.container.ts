import { ChangeDetectionStrategy, Component, computed, inject, linkedSignal } from '@angular/core';
import { Store } from '@ngrx/store';
import { SectionNavComponent } from '@shared';
import { AgesEditorComponent } from '../../components/ages-editor/ages-editor.component';
import { SaveBarComponent } from '../../components/save-bar/save-bar.component';
import { SettingsBannersComponent } from '../../components/settings-banners/settings-banners.component';
import { AgeStep } from '../../models';
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

/** Ages (S-2, ST-2): the "Shop by age" steps. Codes are fixed once saved; labels, order and visibility are free. */
@Component({
  selector: 'app-ages-page',
  imports: [HasPermissionDirective, SectionNavComponent, AgesEditorComponent, SaveBarComponent, SettingsBannersComponent],
  templateUrl: './ages-page.container.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AgesPageContainer implements HasUnsavedChanges {
  private readonly store = inject(Store);

  readonly links = STOREFRONT_LINKS;
  readonly draft = this.store.selectSignal(selectDraft);
  readonly loading = this.store.selectSignal(selectLoading);
  readonly loadError = this.store.selectSignal(selectLoadError);
  readonly saving = this.store.selectSignal(selectSaving);
  readonly saveError = this.store.selectSignal(selectSaveError);
  readonly notice = this.store.selectSignal(selectNotice);

  readonly steps = linkedSignal<readonly AgeStep[]>(() => this.draft()?.ages ?? []);
  readonly lockedCodes = computed(() => (this.draft()?.ages ?? []).map((a) => a.code));
  readonly dirty = computed(() => JSON.stringify(this.steps()) !== JSON.stringify(this.draft()?.ages ?? []));
  readonly fieldErrors = computed(() => this.saveError()?.fieldErrors ?? {});

  constructor() {
    this.store.dispatch(StorefrontSettingsPageActions.draftOpened());
  }

  hasUnsavedChanges(): boolean {
    return this.dirty();
  }

  add(): void {
    this.steps.update((steps) => withSort([...steps, { code: '', label: '', sort: steps.length + 1, enabled: true }]));
  }

  discard(): void {
    this.steps.set(this.draft()?.ages ?? []);
  }

  save(): void {
    this.store.dispatch(StorefrontSettingsPageActions.saveSubmitted({ part: { part: 'ages', steps: withSort(this.steps()) } }));
  }

  reload(): void {
    this.store.dispatch(StorefrontSettingsPageActions.draftOpened());
  }

  dismissNotice(): void {
    this.store.dispatch(StorefrontSettingsPageActions.noticeDismissed());
  }
}
