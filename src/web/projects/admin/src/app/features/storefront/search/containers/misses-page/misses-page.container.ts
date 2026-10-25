import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { Store } from '@ngrx/store';
import { PaginationComponent, SectionNavComponent } from '@shared';
import { STOREFRONT_LINKS } from '../../../storefront-settings/storefront-settings.constants';
import { MissTableComponent } from '../../components/miss-table/miss-table.component';
import { SEARCH_LINKS } from '../../search.constants';
import {
  SearchAdminPageActions,
  selectMisses,
  selectMissesError,
  selectMissesLoading,
  selectMissesPage,
  selectMissesPageSize,
  selectMissesTotal,
} from '../../store';

/** Zero-result searches (SRC-13): what shoppers looked for and did not find, most frequent first. Text only; kept for 180 days. */
@Component({
  selector: 'app-misses-page',
  imports: [SectionNavComponent, MissTableComponent, PaginationComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h1>Search</h1>
    <p class="page-intro">
      Searches that found nothing, most frequent first. Use them to add a synonym or to see what shoppers want. Only the words are kept: no
      name, address, phone number or account is ever recorded, and entries older than 180 days are removed.
    </p>

    <app-section-nav label="Storefront" [links]="storefrontLinks" />
    <app-section-nav label="Search" [links]="searchLinks" />

    @if (error()) {
      <p class="banner banner--error" role="alert">{{ error()?.message }}</p>
    }

    <app-miss-table [misses]="misses()" [loading]="loading()" />
    <app-pagination [total]="total()" [page]="currentPage()" [pageSize]="pageSize()" (pageChanged)="changePage($event)" (pageSizeChanged)="changePageSize($event)" />
  `,
})
export class MissesPageContainer {
  private readonly store = inject(Store);

  readonly storefrontLinks = STOREFRONT_LINKS;
  readonly searchLinks = SEARCH_LINKS;

  readonly misses = this.store.selectSignal(selectMisses);
  readonly total = this.store.selectSignal(selectMissesTotal);
  readonly currentPage = this.store.selectSignal(selectMissesPage);
  readonly pageSize = this.store.selectSignal(selectMissesPageSize);
  readonly loading = this.store.selectSignal(selectMissesLoading);
  readonly error = this.store.selectSignal(selectMissesError);

  constructor() {
    this.store.dispatch(SearchAdminPageActions.missesOpened());
  }

  changePage(page: number): void {
    this.store.dispatch(SearchAdminPageActions.missesPageChanged({ page }));
  }

  changePageSize(pageSize: number): void {
    this.store.dispatch(SearchAdminPageActions.missesPageSizeChanged({ pageSize }));
  }
}
