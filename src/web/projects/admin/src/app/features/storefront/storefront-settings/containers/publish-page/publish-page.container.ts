import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { Store } from '@ngrx/store';
import { PaginationComponent, SectionNavComponent, inputValue } from '@shared';
import { SnapshotsTableComponent } from '../../components/snapshots-table/snapshots-table.component';
import { StorefrontSettingsApiService } from '../../services/storefront-settings-api.service';
import { STOREFRONT_LINKS } from '../../storefront-settings.constants';
import {
  StorefrontSettingsPageActions,
  selectDraft,
  selectLoadError,
  selectPublishError,
  selectPublishNotice,
  selectPublishing,
  selectRestoringVersion,
  selectSnapshots,
  selectSnapshotsLoading,
  selectSnapshotsPage,
  selectSnapshotsPageSize,
  selectSnapshotsTotal,
} from '../../store';
import { flatten } from '../../utils/nav-tree.util';

/** One line of the report the server gives when it will not publish: where, and what to do. */
export interface PublishProblem {
  readonly where: string;
  readonly messages: readonly string[];
}

/**
 * Publish (ST-3, ST-12, ST-17): checks the whole draft, makes it the live version and lists every version so a bad one can be undone.
 * Nothing the admin edits is public until this is pressed; if something is wrong the report says where.
 */
@Component({
  selector: 'app-publish-page',
  imports: [SectionNavComponent, SnapshotsTableComponent, PaginationComponent],
  templateUrl: './publish-page.container.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PublishPageContainer {
  private readonly store = inject(Store);
  private readonly api = inject(StorefrontSettingsApiService);

  readonly links = STOREFRONT_LINKS;
  readonly draft = this.store.selectSignal(selectDraft);
  readonly loadError = this.store.selectSignal(selectLoadError);
  readonly snapshots = this.store.selectSignal(selectSnapshots);
  readonly total = this.store.selectSignal(selectSnapshotsTotal);
  readonly currentPage = this.store.selectSignal(selectSnapshotsPage);
  readonly pageSize = this.store.selectSignal(selectSnapshotsPageSize);
  readonly snapshotsLoading = this.store.selectSignal(selectSnapshotsLoading);
  readonly publishing = this.store.selectSignal(selectPublishing);
  readonly restoringVersion = this.store.selectSignal(selectRestoringVersion);
  readonly error = this.store.selectSignal(selectPublishError);
  readonly notice = this.store.selectSignal(selectPublishNotice);

  readonly previewUrl = this.api.previewUrl();

  readonly note = signal('');
  /** The version waiting for a second click, so a restore is never one stray press. */
  readonly restoreCandidate = signal<number | null>(null);

  readonly summary = computed(() => {
    const draft = this.draft();
    if (!draft) {
      return null;
    }

    const count = (status: string) => draft.pages.filter((p) => p.status === status).length;
    return {
      entries: flatten(draft.navigation).length,
      published: count('Published'),
      drafts: count('Draft'),
      archived: count('Archived'),
      version: draft.version,
    };
  });

  /** Everything the server refused to publish, one entry per place, in plain words. */
  readonly problems = computed<readonly PublishProblem[]>(() =>
    Object.entries(this.error()?.fieldErrors ?? {}).map(([field, messages]) => ({ where: field.split('.').join(' › '), messages })),
  );

  constructor() {
    this.store.dispatch(StorefrontSettingsPageActions.publishOpened());
  }

  noteInput(event: Event): void {
    this.note.set(inputValue(event));
  }

  publish(): void {
    this.store.dispatch(StorefrontSettingsPageActions.publishSubmitted({ note: this.note().trim() }));
  }

  askRestore(versionNo: number): void {
    this.restoreCandidate.set(versionNo);
  }

  cancelRestore(): void {
    this.restoreCandidate.set(null);
  }

  confirmRestore(): void {
    const versionNo = this.restoreCandidate();
    if (versionNo !== null) {
      this.store.dispatch(StorefrontSettingsPageActions.restoreSubmitted({ versionNo, note: this.note().trim() }));
      this.restoreCandidate.set(null);
    }
  }

  changePage(page: number): void {
    this.store.dispatch(StorefrontSettingsPageActions.snapshotsPageChanged({ page }));
  }

  dismissNotice(): void {
    this.store.dispatch(StorefrontSettingsPageActions.noticeDismissed());
  }
}
