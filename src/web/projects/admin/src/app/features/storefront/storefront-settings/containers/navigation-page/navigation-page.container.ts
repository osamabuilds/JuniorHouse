import { ChangeDetectionStrategy, Component, computed, inject, linkedSignal } from '@angular/core';
import { Store } from '@ngrx/store';
import { SectionNavComponent } from '@shared';
import { NavNodeComponent } from '../../components/nav-node/nav-node.component';
import { SaveBarComponent } from '../../components/save-bar/save-bar.component';
import { SettingsBannersComponent } from '../../components/settings-banners/settings-banners.component';
import { NavAction, NavItem } from '../../models';
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
import { addRoot, applyNavAction, newEntry } from '../../utils/nav-tree.util';
import { HasUnsavedChanges } from '../../utils/unsaved-changes.guard';

/**
 * Navigation (docs/specs/storefront/storefront-settings/spec.md, S-1, ST-1, ST-1a): the header entries as a tree of up to three levels.
 * The edits live here until Save; every action is a button (ST-15). Nothing reaches the shop until Publish (ST-3).
 */
@Component({
  selector: 'app-navigation-page',
  imports: [SectionNavComponent, NavNodeComponent, SaveBarComponent, SettingsBannersComponent],
  templateUrl: './navigation-page.container.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class NavigationPageContainer implements HasUnsavedChanges {
  private readonly store = inject(Store);

  readonly links = STOREFRONT_LINKS;
  readonly draft = this.store.selectSignal(selectDraft);
  readonly loading = this.store.selectSignal(selectLoading);
  readonly loadError = this.store.selectSignal(selectLoadError);
  readonly saving = this.store.selectSignal(selectSaving);
  readonly saveError = this.store.selectSignal(selectSaveError);
  readonly notice = this.store.selectSignal(selectNotice);

  /** The tree being edited. It starts from the saved draft and starts over whenever a new draft arrives (after a save or a reload). */
  readonly items = linkedSignal<readonly NavItem[]>(() => this.draft()?.navigation ?? []);

  readonly dirty = computed(() => JSON.stringify(this.items()) !== JSON.stringify(this.draft()?.navigation ?? []));
  readonly fieldErrors = computed(() => this.saveError()?.fieldErrors ?? {});

  constructor() {
    this.store.dispatch(StorefrontSettingsPageActions.draftOpened());
  }

  hasUnsavedChanges(): boolean {
    return this.dirty();
  }

  onAction(action: NavAction): void {
    this.items.update((items) => applyNavAction(items, action));
  }

  addEntry(): void {
    this.items.update((items) => addRoot(items, newEntry(items, 'New entry')));
  }

  discard(): void {
    this.items.set(this.draft()?.navigation ?? []);
  }

  save(): void {
    this.store.dispatch(StorefrontSettingsPageActions.saveSubmitted({ part: { part: 'navigation', items: this.items() } }));
  }

  reload(): void {
    this.store.dispatch(StorefrontSettingsPageActions.draftOpened());
  }

  dismissNotice(): void {
    this.store.dispatch(StorefrontSettingsPageActions.noticeDismissed());
  }
}
