import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { Store } from '@ngrx/store';
import { PaginationComponent } from '@shared';
import { NotifyDemandTableComponent } from '../../components/notify-demand-table/notify-demand-table.component';
import {
  NotifyDemandPageActions,
  selectError,
  selectLoading,
  selectPage,
  selectPageSize,
  selectRows,
  selectTotal,
} from '../../store';

/**
 * The Notify Me demand screen (docs/specs/customers/notify-me, NTM-11, NTM-12): which sizes have customers waiting, most waiting first.
 * Read-only and counts only; it never shows an email address. The numbers are a demand signal for reordering and production planning.
 */
@Component({
  selector: 'app-notify-demand-page',
  imports: [PaginationComponent, NotifyDemandTableComponent],
  templateUrl: './notify-demand-page.container.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class NotifyDemandPageContainer {
  private readonly store = inject(Store);

  readonly rows = this.store.selectSignal(selectRows);
  readonly total = this.store.selectSignal(selectTotal);
  readonly currentPage = this.store.selectSignal(selectPage);
  readonly pageSize = this.store.selectSignal(selectPageSize);
  readonly loading = this.store.selectSignal(selectLoading);
  readonly error = this.store.selectSignal(selectError);

  constructor() {
    this.store.dispatch(NotifyDemandPageActions.opened());
  }

  refresh(): void {
    this.store.dispatch(NotifyDemandPageActions.refreshed());
  }

  changePage(page: number): void {
    this.store.dispatch(NotifyDemandPageActions.pageChanged({ page }));
  }

  changePageSize(pageSize: number): void {
    this.store.dispatch(NotifyDemandPageActions.pageSizeChanged({ pageSize }));
  }
}
