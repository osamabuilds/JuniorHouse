import { ChangeDetectionStrategy, Component, effect, inject, input, untracked } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Store } from '@ngrx/store';
import { PaginationComponent, inputValue, selectNumberOrNull } from '@shared';
import { BATCH_STATUS_LABELS, INSPECTION_RESULT_LABELS } from '../../quality.constants';
import {
  BatchListActions,
  selectBatchSearch,
  selectBatchStatusFilter,
  selectBatches,
  selectBatchesError,
  selectBatchesLoading,
  selectBatchesPage,
  selectBatchesPageSize,
  selectBatchesTotal,
} from '../../store';

/**
 * Sprint 4: every QC batch and where it stands (BRD 5.11). A goods receipt opens one batch; open it to inspect, record the child-safety
 * checks and decide the outcome. A `search` query parameter presets the search (the receive page links here with the GRN number).
 */
@Component({
  selector: 'app-batches-page',
  imports: [RouterLink, PaginationComponent],
  template: `
    <p><a routerLink="/quality">&larr; Receiving queue</a></p>
    <h1>QC batches</h1>
    <p class="page-intro">
      Every goods receipt opens one batch to inspect. Only accepted quantity is released; rejected quantity never is.
    </p>

    <div class="toolbar">
      <label class="toolbar__search">
        <span class="sr-only">Search by GRN, purchase order or delivery note number</span>
        <input type="search" placeholder="Search by GRN, PO or delivery note number" [value]="searchText()" (input)="onSearchInput($event)" />
      </label>

      <label class="toolbar__select">
        <span class="sr-only">Batch status</span>
        <select (change)="onStatusChange($event)">
          <option value="" [selected]="statusFilter() === null">All batches</option>
          @for (status of statuses; track status.id) {
            <option [value]="status.id" [selected]="statusFilter() === status.id">{{ status.name }}</option>
          }
        </select>
      </label>
    </div>

    @if (error()) {
      <p class="banner banner--error" role="alert">{{ error()?.message }}</p>
    }

    @if (loading()) {
      <p class="state-message">Loading…</p>
    } @else if (batches().length === 0) {
      <p class="state-message">No QC batches match. A batch opens as soon as a goods receipt is recorded.</p>
    } @else {
      <table class="responsive">
        <caption class="sr-only">
          QC batches
        </caption>
        <thead>
          <tr>
            <th scope="col">Goods receipt</th>
            <th scope="col">Purchase order</th>
            <th scope="col">Delivery note</th>
            <th scope="col">Pieces</th>
            <th scope="col">Status</th>
            <th scope="col">Inspection</th>
          </tr>
        </thead>
        <tbody>
          @for (batch of batches(); track batch.id) {
            <tr>
              <td data-label="Goods receipt">
                <a [routerLink]="['/quality/batches', batch.id]">{{ batch.grnNo }}</a>
              </td>
              <td data-label="Purchase order">{{ batch.poNo }}</td>
              <td data-label="Delivery note">{{ batch.vendorDnNo }}</td>
              <td data-label="Pieces">{{ batch.lotQty }}</td>
              <td data-label="Status">
                <span class="badge" [class.badge--muted]="batch.statusId === 3 || batch.statusId === 5">{{ statusLabels[batch.statusId] }}</span>
              </td>
              <td data-label="Inspection">
                @if (batch.latestResultId === null) {
                  Not inspected yet
                } @else {
                  {{ batch.latestResultId === 1 ? resultLabels['Pass'] : resultLabels['Fail'] }}
                }
              </td>
            </tr>
          }
        </tbody>
      </table>
    }

    <app-pagination
      [total]="total()"
      [page]="currentPage()"
      [pageSize]="pageSize()"
      (pageChanged)="changePage($event)"
      (pageSizeChanged)="changePageSize($event)"
    />
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class BatchesPageContainer {
  private readonly store = inject(Store);

  /** Presets the search from the route's query string (withComponentInputBinding). */
  readonly search = input<string | undefined>(undefined);

  readonly batches = this.store.selectSignal(selectBatches);
  readonly total = this.store.selectSignal(selectBatchesTotal);
  readonly currentPage = this.store.selectSignal(selectBatchesPage);
  readonly pageSize = this.store.selectSignal(selectBatchesPageSize);
  readonly loading = this.store.selectSignal(selectBatchesLoading);
  readonly error = this.store.selectSignal(selectBatchesError);
  readonly searchText = this.store.selectSignal(selectBatchSearch);
  readonly statusFilter = this.store.selectSignal(selectBatchStatusFilter);

  readonly statuses = Object.entries(BATCH_STATUS_LABELS).map(([id, name]) => ({ id: +id, name }));
  readonly statusLabels = BATCH_STATUS_LABELS;
  readonly resultLabels = INSPECTION_RESULT_LABELS;

  constructor() {
    effect(() => {
      const search = this.search();
      untracked(() => {
        this.store.dispatch(BatchListActions.opened());
        if (search) {
          this.store.dispatch(BatchListActions.searchChanged({ searchText: search }));
        }
      });
    });
  }

  onSearchInput(event: Event): void {
    this.store.dispatch(BatchListActions.searchChanged({ searchText: inputValue(event) }));
  }

  onStatusChange(event: Event): void {
    this.store.dispatch(BatchListActions.statusFilterChanged({ statusId: selectNumberOrNull(event) }));
  }

  changePage(page: number): void {
    this.store.dispatch(BatchListActions.pageChanged({ page }));
  }

  changePageSize(size: number): void {
    this.store.dispatch(BatchListActions.pageSizeChanged({ pageSize: size }));
  }
}
