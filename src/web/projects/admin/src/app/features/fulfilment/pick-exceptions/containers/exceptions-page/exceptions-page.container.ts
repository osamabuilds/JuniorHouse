import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { Store } from '@ngrx/store';
import { holdsAny, selectPermissions } from '@core/auth';
import { PaginationComponent } from '@shared';
import { ExceptionsTableComponent, ResolveRequest } from '../../components/exceptions-table/exceptions-table.component';
import {
  ExceptionsPageActions,
  selectError,
  selectLoading,
  selectNotice,
  selectOpenOnly,
  selectPage,
  selectPageSize,
  selectResolvingId,
  selectRows,
  selectTotal,
} from '../../store';

/**
 * The pick exception queue (docs/specs/fulfilment/pick-exceptions, PEX-4, PEX-8, PEX-9): what pickers flagged, newest last, with who and when.
 * Staff with fulfilment.manage close an exception once the shelf is right again, or drop the line when the customer would rather not wait.
 */
@Component({
  selector: 'app-exceptions-page',
  imports: [PaginationComponent, ExceptionsTableComponent],
  templateUrl: './exceptions-page.container.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ExceptionsPageContainer {
  private readonly store = inject(Store);

  readonly rows = this.store.selectSignal(selectRows);
  readonly total = this.store.selectSignal(selectTotal);
  readonly currentPage = this.store.selectSignal(selectPage);
  readonly pageSize = this.store.selectSignal(selectPageSize);
  readonly openOnly = this.store.selectSignal(selectOpenOnly);
  readonly loading = this.store.selectSignal(selectLoading);
  readonly resolvingId = this.store.selectSignal(selectResolvingId);
  readonly error = this.store.selectSignal(selectError);
  readonly notice = this.store.selectSignal(selectNotice);
  private readonly permissions = this.store.selectSignal(selectPermissions);
  /** Only tidies the screen: the server checks fulfilment.manage again on every call (SEC-08). */
  readonly canResolve = computed(() => holdsAny(this.permissions(), ['fulfilment.manage']));

  constructor() {
    this.store.dispatch(ExceptionsPageActions.opened());
  }

  toggleOpenOnly(event: Event): void {
    this.store.dispatch(ExceptionsPageActions.filterChanged({ openOnly: (event.target as HTMLInputElement).checked }));
  }

  changePage(page: number): void {
    this.store.dispatch(ExceptionsPageActions.pageChanged({ page }));
  }

  changePageSize(pageSize: number): void {
    this.store.dispatch(ExceptionsPageActions.pageSizeChanged({ pageSize }));
  }

  refresh(): void {
    this.store.dispatch(ExceptionsPageActions.refreshed());
  }

  resolve(request: ResolveRequest): void {
    this.store.dispatch(ExceptionsPageActions.resolveConfirmed(request));
  }

  dismissNotice(): void {
    this.store.dispatch(ExceptionsPageActions.noticeDismissed());
  }
}
