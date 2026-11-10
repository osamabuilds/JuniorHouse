import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Store } from '@ngrx/store';
import { DEFAULT_PAGE_SIZE, PaginationComponent } from '@shared';
import { STATUS_FILTERS, statusWord } from '../../models';
import { CasesPageActions, selectCases, selectError, selectLoading, selectPage, selectStatus, selectTotal } from '../../store';

/** The returned parcels, newest first, filtered by where they are in the gate (docs/specs/returns/reverse-qc, RQC-11). */
@Component({
  selector: 'app-return-cases-page',
  imports: [DatePipe, RouterLink, PaginationComponent],
  templateUrl: './cases-page.container.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CasesPageContainer {
  private readonly store = inject(Store);

  readonly cases = this.store.selectSignal(selectCases);
  readonly total = this.store.selectSignal(selectTotal);
  readonly page = this.store.selectSignal(selectPage);
  readonly status = this.store.selectSignal(selectStatus);
  readonly loading = this.store.selectSignal(selectLoading);
  readonly error = this.store.selectSignal(selectError);

  readonly pageSize = DEFAULT_PAGE_SIZE;
  readonly filters = STATUS_FILTERS;
  readonly word = statusWord;

  constructor() {
    this.store.dispatch(CasesPageActions.opened());
  }

  changeStatus(event: Event): void {
    this.store.dispatch(CasesPageActions.statusChanged({ status: (event.target as HTMLSelectElement).value }));
  }

  changePage(page: number): void {
    this.store.dispatch(CasesPageActions.pageChanged({ page }));
  }
}
