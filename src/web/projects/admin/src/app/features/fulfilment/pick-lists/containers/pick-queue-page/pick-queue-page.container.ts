import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { Router } from '@angular/router';
import { Store } from '@ngrx/store';
import { selectSessionUser } from '@core/auth';
import { PaginationComponent } from '@shared';
import { QueueTableComponent } from '../../components/queue-table/queue-table.component';
import { PickQueuePageActions, selectError, selectPage, selectPageSize, selectQueue, selectQueueLoading, selectTotal } from '../../store';

/**
 * The pick queue (docs/specs/fulfilment/pick-lists, PCK-1, APP-8): orders waiting to be picked, oldest first, with how long each has waited. A
 * picker opens one to take it. The list is paged and refreshes without a full reload.
 */
@Component({
  selector: 'app-pick-queue-page',
  imports: [PaginationComponent, QueueTableComponent],
  templateUrl: './pick-queue-page.container.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PickQueuePageContainer {
  private readonly store = inject(Store);
  private readonly router = inject(Router);

  readonly rows = this.store.selectSignal(selectQueue);
  readonly total = this.store.selectSignal(selectTotal);
  readonly currentPage = this.store.selectSignal(selectPage);
  readonly pageSize = this.store.selectSignal(selectPageSize);
  readonly loading = this.store.selectSignal(selectQueueLoading);
  readonly error = this.store.selectSignal(selectError);
  private readonly user = this.store.selectSignal(selectSessionUser);
  readonly me = computed(() => this.user()?.userName ?? null);

  constructor() {
    this.store.dispatch(PickQueuePageActions.opened());
  }

  open(orderId: number): void {
    void this.router.navigate(['/fulfilment/pick', orderId]);
  }

  changePage(page: number): void {
    this.store.dispatch(PickQueuePageActions.pageChanged({ page }));
  }

  changePageSize(pageSize: number): void {
    this.store.dispatch(PickQueuePageActions.pageSizeChanged({ pageSize }));
  }

  refresh(): void {
    this.store.dispatch(PickQueuePageActions.refreshed());
  }
}
