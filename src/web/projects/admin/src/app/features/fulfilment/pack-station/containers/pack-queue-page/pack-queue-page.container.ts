import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { Router } from '@angular/router';
import { Store } from '@ngrx/store';
import { selectSessionUser } from '@core/auth';
import { PaginationComponent } from '@shared';
import { QueueTableComponent } from '../../../pick-lists';
import { PackQueuePageActions, selectError, selectPage, selectPageSize, selectQueue, selectQueueLoading, selectTotal } from '../../store';

/**
 * The pack queue (docs/specs/fulfilment/pack-station, PAK-10): orders that are fully picked and waiting for a packer, oldest first, with how long
 * each has waited. An order that is not fully picked never appears here.
 */
@Component({
  selector: 'app-pack-queue-page',
  imports: [PaginationComponent, QueueTableComponent],
  templateUrl: './pack-queue-page.container.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PackQueuePageContainer {
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
    this.store.dispatch(PackQueuePageActions.opened());
  }

  open(orderId: number): void {
    void this.router.navigate(['/fulfilment/pack', orderId]);
  }

  changePage(page: number): void {
    this.store.dispatch(PackQueuePageActions.pageChanged({ page }));
  }

  changePageSize(pageSize: number): void {
    this.store.dispatch(PackQueuePageActions.pageSizeChanged({ pageSize }));
  }

  refresh(): void {
    this.store.dispatch(PackQueuePageActions.refreshed());
  }
}
