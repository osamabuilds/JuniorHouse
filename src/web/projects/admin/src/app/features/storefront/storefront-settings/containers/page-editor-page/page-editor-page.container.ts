import { ChangeDetectionStrategy, Component, computed, inject, linkedSignal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Store } from '@ngrx/store';
import { inputValue } from '@shared';
import { PageBlocksEditorComponent } from '../../components/page-blocks-editor/page-blocks-editor.component';
import { SaveBarComponent } from '../../components/save-bar/save-bar.component';
import { ContentPage, PageStatus, PageValue } from '../../models';
import { StorefrontSettingsApiService } from '../../services/storefront-settings-api.service';
import { MAX_META_DESCRIPTION_LENGTH } from '../../storefront-settings.constants';
import {
  StorefrontSettingsPageActions,
  selectPage,
  selectPageError,
  selectPageLoading,
  selectPageNotice,
  selectPageSaving,
} from '../../store';
import { cleanBlocks } from '../../utils/blocks.util';
import { HasUnsavedChanges } from '../../utils/unsaved-changes.guard';

const BLANK: PageValue = { slug: '', title: '', metaDescription: '', blocks: [] };

function toValue(page: ContentPage | null): PageValue {
  return page ? { slug: page.slug, title: page.title, metaDescription: page.metaDescription, blocks: page.blocks } : BLANK;
}

/**
 * One content page (ST-8, ST-9, ST-10, ST-17): title, address, search description and a body of blocks, plus its status. Changing the
 * address of a live page sends the old address on to the new one; publishing a page that quotes an empty value is refused with the reason.
 */
@Component({
  selector: 'app-page-editor-page',
  imports: [RouterLink, PageBlocksEditorComponent, SaveBarComponent],
  templateUrl: './page-editor-page.container.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PageEditorPageContainer implements HasUnsavedChanges {
  private readonly store = inject(Store);
  private readonly api = inject(StorefrontSettingsApiService);

  readonly id = Number(inject(ActivatedRoute).snapshot.paramMap.get('id')) || null;
  readonly page = this.store.selectSignal(selectPage);
  readonly loading = this.store.selectSignal(selectPageLoading);
  readonly saving = this.store.selectSignal(selectPageSaving);
  readonly error = this.store.selectSignal(selectPageError);
  readonly notice = this.store.selectSignal(selectPageNotice);

  readonly maxDescription = MAX_META_DESCRIPTION_LENGTH;
  readonly previewUrl = this.api.previewUrl();

  readonly value = linkedSignal<PageValue>(() => toValue(this.page()));
  readonly dirty = computed(() => JSON.stringify(this.value()) !== JSON.stringify(toValue(this.page())));
  readonly isNew = computed(() => this.page() === null);

  /** Every message the server gave for this page, collected so none is hidden behind a field that is not on screen. */
  readonly problems = computed(() => [...new Set(Object.values(this.error()?.fieldErrors ?? {}).flat())]);

  /** A live page that is renamed leaves its old address answering with a redirect. */
  readonly redirectNote = computed(() => {
    const page = this.page();
    const slug = this.value().slug.trim().toLowerCase();
    return page && page.status !== 'Draft' && slug && slug !== page.slug ? `Anyone who opens /${page.slug} will be sent on to /${slug}.` : null;
  });

  constructor() {
    this.store.dispatch(StorefrontSettingsPageActions.pageEditorOpened({ id: this.id }));
  }

  hasUnsavedChanges(): boolean {
    return this.dirty();
  }

  edit(patch: Partial<PageValue>): void {
    this.value.update((value) => ({ ...value, ...patch }));
  }

  discard(): void {
    this.value.set(toValue(this.page()));
  }

  save(): void {
    const value = this.value();
    this.store.dispatch(
      StorefrontSettingsPageActions.pageSaveSubmitted({
        value: { ...value, slug: value.slug.trim().toLowerCase(), title: value.title.trim(), metaDescription: value.metaDescription.trim(), blocks: cleanBlocks(value.blocks) },
      }),
    );
  }

  setStatus(status: PageStatus): void {
    this.store.dispatch(StorefrontSettingsPageActions.pageStatusSubmitted({ status }));
  }

  dismissNotice(): void {
    this.store.dispatch(StorefrontSettingsPageActions.noticeDismissed());
  }

  text(event: Event): string {
    return inputValue(event);
  }
}
