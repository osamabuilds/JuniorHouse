import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Store } from '@ngrx/store';
import { PaginationComponent } from '@shared';
import { statusWords } from '../../models';
import { ManifestsPageActions, selectBusy, selectError, selectLoading, selectNotice, selectPage, selectPageSize, selectRows, selectTotal } from '../../store';

/**
 * The manifests (docs/specs/shipping/manifests, MNF-1, MNF-4, MNF-10): one sheet per courier and day, newest first and paged. Staff can make the
 * day's manifests now; the worker also makes them at the end of the day.
 */
@Component({
  selector: 'app-manifests-page',
  imports: [DatePipe, RouterLink, PaginationComponent],
  templateUrl: './manifests-page.container.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ManifestsPageContainer {
  private readonly store = inject(Store);

  readonly rows = this.store.selectSignal(selectRows);
  readonly total = this.store.selectSignal(selectTotal);
  readonly currentPage = this.store.selectSignal(selectPage);
  readonly pageSize = this.store.selectSignal(selectPageSize);
  readonly loading = this.store.selectSignal(selectLoading);
  readonly busy = this.store.selectSignal(selectBusy);
  readonly error = this.store.selectSignal(selectError);
  readonly notice = this.store.selectSignal(selectNotice);

  readonly words = statusWords;

  constructor() {
    this.store.dispatch(ManifestsPageActions.opened());
  }

  generate(): void {
    this.store.dispatch(ManifestsPageActions.generateClicked());
  }

  changePage(page: number): void {
    this.store.dispatch(ManifestsPageActions.pageChanged({ page }));
  }

  changePageSize(pageSize: number): void {
    this.store.dispatch(ManifestsPageActions.pageSizeChanged({ pageSize }));
  }

  dismissNotice(): void {
    this.store.dispatch(ManifestsPageActions.noticeDismissed());
  }
}
