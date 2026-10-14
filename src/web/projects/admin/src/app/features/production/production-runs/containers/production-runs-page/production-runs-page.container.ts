import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { Store } from '@ngrx/store';
import { PaginationComponent, inputChecked, inputValue, selectNumberOrNull } from '@shared';
import { RunTableComponent } from '../../components/run-table/run-table.component';
import { RUN_STATUS_LABELS } from '../../production-runs.constants';
import {
  ProductionPageActions,
  selectAtRiskOnly,
  selectRuns,
  selectRunsError,
  selectRunsLoading,
  selectRunsPage,
  selectRunsPageSize,
  selectRunsTotal,
  selectSearchText,
  selectStatusFilter,
} from '../../store';

/**
 * Sprint 3: the production list (BRD 5.10): every acknowledged purchase order and where it stands - sample, cutting, expected
 * completion, what has shipped - with warnings written out. Filters change the search and go back to page 1.
 */
@Component({
  selector: 'app-production-runs-page',
  imports: [RunTableComponent, PaginationComponent],
  templateUrl: './production-runs-page.container.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProductionRunsPageContainer {
  private readonly store = inject(Store);

  readonly runs = this.store.selectSignal(selectRuns);
  readonly total = this.store.selectSignal(selectRunsTotal);
  readonly currentPage = this.store.selectSignal(selectRunsPage);
  readonly pageSize = this.store.selectSignal(selectRunsPageSize);
  readonly loading = this.store.selectSignal(selectRunsLoading);
  readonly error = this.store.selectSignal(selectRunsError);
  readonly searchText = this.store.selectSignal(selectSearchText);
  readonly statusFilter = this.store.selectSignal(selectStatusFilter);
  readonly atRiskOnly = this.store.selectSignal(selectAtRiskOnly);

  readonly statuses = Object.entries(RUN_STATUS_LABELS).map(([id, name]) => ({ id: +id, name }));

  constructor() {
    this.store.dispatch(ProductionPageActions.listOpened());
  }

  onSearchInput(event: Event): void {
    this.store.dispatch(ProductionPageActions.searchChanged({ searchText: inputValue(event) }));
  }

  onStatusChange(event: Event): void {
    this.store.dispatch(ProductionPageActions.statusFilterChanged({ statusId: selectNumberOrNull(event) }));
  }

  onAtRiskChange(event: Event): void {
    if (inputChecked(event) !== this.atRiskOnly()) {
      this.store.dispatch(ProductionPageActions.atRiskToggled());
    }
  }

  changePage(page: number): void {
    this.store.dispatch(ProductionPageActions.pageChanged({ page }));
  }

  changePageSize(size: number): void {
    this.store.dispatch(ProductionPageActions.pageSizeChanged({ pageSize: size }));
  }
}
