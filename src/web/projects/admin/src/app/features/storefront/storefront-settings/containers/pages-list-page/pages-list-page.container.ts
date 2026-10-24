import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Store } from '@ngrx/store';
import { PaginationComponent, SectionNavComponent, selectValue } from '@shared';
import { PagesTableComponent } from '../../components/pages-table/pages-table.component';
import { PageStatus } from '../../models';
import { PAGE_STATUS_OPTIONS, STOREFRONT_LINKS } from '../../storefront-settings.constants';
import {
  StorefrontSettingsPageActions,
  selectPages,
  selectPagesError,
  selectPagesLoading,
  selectPagesPage,
  selectPagesPageSize,
  selectPagesStatus,
  selectPagesTotal,
} from '../../store';

/** Pages (ST-8): the content pages (delivery, exchanges, size guide, ...), filtered by status and paged. */
@Component({
  selector: 'app-pages-list-page',
  imports: [RouterLink, SectionNavComponent, PagesTableComponent, PaginationComponent],
  templateUrl: './pages-list-page.container.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PagesListPageContainer {
  private readonly store = inject(Store);

  readonly links = STOREFRONT_LINKS;
  readonly statuses = PAGE_STATUS_OPTIONS;
  readonly pages = this.store.selectSignal(selectPages);
  readonly total = this.store.selectSignal(selectPagesTotal);
  readonly currentPage = this.store.selectSignal(selectPagesPage);
  readonly pageSize = this.store.selectSignal(selectPagesPageSize);
  readonly status = this.store.selectSignal(selectPagesStatus);
  readonly loading = this.store.selectSignal(selectPagesLoading);
  readonly error = this.store.selectSignal(selectPagesError);

  constructor() {
    this.store.dispatch(StorefrontSettingsPageActions.pagesOpened());
  }

  filter(event: Event): void {
    this.store.dispatch(StorefrontSettingsPageActions.pagesStatusFiltered({ status: selectValue(event) as PageStatus | '' }));
  }

  changePage(page: number): void {
    this.store.dispatch(StorefrontSettingsPageActions.pagesPageChanged({ page }));
  }

  changePageSize(pageSize: number): void {
    this.store.dispatch(StorefrontSettingsPageActions.pagesPageSizeChanged({ pageSize }));
  }
}
