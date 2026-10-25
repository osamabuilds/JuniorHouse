import { ChangeDetectionStrategy, Component, effect, inject, untracked } from '@angular/core';
import { FormBuilder } from '@angular/forms';
import { Store } from '@ngrx/store';
import { missingSummary, PaginationComponent, SectionNavComponent } from '@shared';
import { STOREFRONT_LINKS } from '../../../storefront-settings/storefront-settings.constants';
import { SynonymFormComponent } from '../../components/synonym-form/synonym-form.component';
import { SynonymTableComponent } from '../../components/synonym-table/synonym-table.component';
import { buildSynonymForm, parseTerms, termsToText } from '../../forms/synonym.form';
import { SynonymGroup } from '../../models';
import { SEARCH_LINKS } from '../../search.constants';
import {
  SearchAdminPageActions,
  selectDeletingId,
  selectEditingGroup,
  selectSearchFormError,
  selectSearchMode,
  selectSearchSaving,
  selectSynonyms,
  selectSynonymsError,
  selectSynonymsLoading,
  selectSynonymsPage,
  selectSynonymsPageSize,
  selectSynonymsTotal,
} from '../../store';

/** Synonym groups (SRC-12): staff add words that mean the same to a shopper, with no deployment. */
@Component({
  selector: 'app-synonyms-page',
  imports: [SectionNavComponent, SynonymTableComponent, SynonymFormComponent, PaginationComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h1>Search</h1>
    <p class="page-intro">
      What shoppers type is matched against product names, categories, collections, colours and codes. Synonym groups teach the search that
      different words mean the same thing, including Roman Urdu spellings.
    </p>

    <app-section-nav label="Storefront" [links]="storefrontLinks" />
    <app-section-nav label="Search" [links]="searchLinks" />

    @if (mode() === 'list') {
      @if (error()) {
        <p class="banner banner--error" role="alert">{{ error()?.message }}</p>
      }

      <div class="toolbar">
        <button type="button" class="button button--primary" (click)="startCreate()">Add synonym group</button>
      </div>

      <app-synonym-table
        [groups]="groups()"
        [loading]="loading()"
        [deletingId]="deletingId()"
        (edit)="startEdit($event)"
        (deleteRequested)="requestDelete($event)"
        (deleteCancelled)="cancelDelete()"
        (deleteConfirmed)="confirmDelete($event)"
      />
      <app-pagination [total]="total()" [page]="currentPage()" [pageSize]="pageSize()" (pageChanged)="changePage($event)" (pageSizeChanged)="changePageSize($event)" />
    } @else {
      <app-synonym-form [form]="form" [mode]="mode()" [saving]="saving()" [error]="formError()" (submitted)="save()" (cancelled)="cancelEdit()" />
    }
  `,
})
export class SynonymsPageContainer {
  private readonly store = inject(Store);
  private readonly formBuilder = inject(FormBuilder);

  readonly storefrontLinks = STOREFRONT_LINKS;
  readonly searchLinks = SEARCH_LINKS;

  readonly groups = this.store.selectSignal(selectSynonyms);
  readonly total = this.store.selectSignal(selectSynonymsTotal);
  readonly currentPage = this.store.selectSignal(selectSynonymsPage);
  readonly pageSize = this.store.selectSignal(selectSynonymsPageSize);
  readonly loading = this.store.selectSignal(selectSynonymsLoading);
  readonly error = this.store.selectSignal(selectSynonymsError);
  readonly mode = this.store.selectSignal(selectSearchMode);
  readonly saving = this.store.selectSignal(selectSearchSaving);
  readonly formError = this.store.selectSignal(selectSearchFormError);
  readonly deletingId = this.store.selectSignal(selectDeletingId);
  private readonly editing = this.store.selectSignal(selectEditingGroup);

  readonly form = buildSynonymForm(this.formBuilder);

  constructor() {
    this.store.dispatch(SearchAdminPageActions.synonymsOpened());

    // The group to edit is chosen in the list; fill the form from it.
    effect(() => {
      const group = this.editing();
      if (group) {
        untracked(() => this.populate(group));
      }
    });
  }

  startCreate(): void {
    this.form.reset({ name: '', isActive: true, terms: '' });
    this.store.dispatch(SearchAdminPageActions.createStarted());
  }

  startEdit(group: SynonymGroup): void {
    this.store.dispatch(SearchAdminPageActions.editStarted({ group }));
  }

  cancelEdit(): void {
    this.store.dispatch(SearchAdminPageActions.editCancelled());
  }

  requestDelete(id: number): void {
    this.store.dispatch(SearchAdminPageActions.deleteRequested({ id }));
  }

  cancelDelete(): void {
    this.store.dispatch(SearchAdminPageActions.deleteCancelled());
  }

  confirmDelete(id: number): void {
    this.store.dispatch(SearchAdminPageActions.deleteConfirmed({ id }));
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.store.dispatch(
        SearchAdminPageActions.formInvalid({
          error: { status: 0, message: missingSummary(this.form.controls, { name: 'Name', terms: 'Words' }, []), fieldErrors: {} },
        }),
      );
      return;
    }

    const raw = this.form.getRawValue();
    const value = { name: raw.name.trim(), isActive: raw.isActive, terms: parseTerms(raw.terms) };
    const group = this.editing();
    this.store.dispatch(
      this.mode() === 'create'
        ? SearchAdminPageActions.createSubmitted({ value })
        : SearchAdminPageActions.updateSubmitted({ id: group!.id, rowVersion: group!.rowVersion, value }),
    );
  }

  changePage(page: number): void {
    this.store.dispatch(SearchAdminPageActions.synonymsPageChanged({ page }));
  }

  changePageSize(pageSize: number): void {
    this.store.dispatch(SearchAdminPageActions.synonymsPageSizeChanged({ pageSize }));
  }

  private populate(group: SynonymGroup): void {
    this.form.reset({ name: group.name, isActive: group.isActive, terms: termsToText(group.terms) });
  }
}
