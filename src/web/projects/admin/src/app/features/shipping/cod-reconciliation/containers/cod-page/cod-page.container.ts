import { CurrencyPipe, DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Store } from '@ngrx/store';
import { DEFAULT_PAGE_SIZE, PaginationComponent } from '@shared';
import { outcomeWords } from '../../models';
import {
  CodListName,
  CodPageActions,
  selectAwaiting,
  selectBusy,
  selectError,
  selectExceptions,
  selectNotice,
  selectOverdueOnly,
  selectRemittances,
  selectSummary,
} from '../../store';

/**
 * COD reconciliation (docs/specs/shipping/cod-reconciliation, COD-11 to COD-14): what each courier owes in cash, the delivered parcels still unpaid
 * (the overdue ones flagged), the statement lines that did not match, and the statements recorded so far.
 */
@Component({
  selector: 'app-cod-page',
  imports: [CurrencyPipe, DatePipe, RouterLink, PaginationComponent],
  templateUrl: './cod-page.container.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CodPageContainer {
  private readonly store = inject(Store);

  readonly summary = this.store.selectSignal(selectSummary);
  readonly awaiting = this.store.selectSignal(selectAwaiting);
  readonly overdueOnly = this.store.selectSignal(selectOverdueOnly);
  readonly exceptions = this.store.selectSignal(selectExceptions);
  readonly remittances = this.store.selectSignal(selectRemittances);
  readonly busy = this.store.selectSignal(selectBusy);
  readonly error = this.store.selectSignal(selectError);
  readonly notice = this.store.selectSignal(selectNotice);

  readonly pageSize = DEFAULT_PAGE_SIZE;
  readonly words = outcomeWords;
  readonly notes = signal<Record<number, string>>({});

  constructor() {
    this.store.dispatch(CodPageActions.opened());
  }

  changePage(list: CodListName, page: number): void {
    this.store.dispatch(CodPageActions.pageChanged({ list, page }));
  }

  toggleOverdue(event: Event): void {
    this.store.dispatch(CodPageActions.overdueFilterChanged({ overdueOnly: (event.target as HTMLInputElement).checked }));
  }

  typeNote(lineId: number, event: Event): void {
    const value = (event.target as HTMLInputElement).value;
    this.notes.update((n) => ({ ...n, [lineId]: value }));
  }

  resolve(lineId: number, event: Event): void {
    event.preventDefault();
    const note = (this.notes()[lineId] ?? '').trim();
    if (note.length > 0) {
      this.store.dispatch(CodPageActions.lineResolved({ lineId, note }));
    }
  }

  dismissNotice(): void {
    this.store.dispatch(CodPageActions.noticeDismissed());
  }
}
