import { CurrencyPipe, DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, effect, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Store } from '@ngrx/store';
import { outcomeWords } from '../../models';
import { CodRemittanceActions, selectBusy, selectDetail, selectError, selectLoading, selectNotice } from '../../store';

/** One recorded statement with the outcome of every line and, for a flagged line, the note that closed it (COD-10, COD-12). */
@Component({
  selector: 'app-remittance-detail-page',
  imports: [CurrencyPipe, DatePipe, RouterLink],
  templateUrl: './remittance-detail-page.container.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RemittanceDetailPageContainer {
  private readonly store = inject(Store);

  /** From the address (`/dispatch/cod/remittances/:id`), bound by the router. */
  readonly id = input.required<string>();

  readonly detail = this.store.selectSignal(selectDetail);
  readonly loading = this.store.selectSignal(selectLoading);
  readonly busy = this.store.selectSignal(selectBusy);
  readonly error = this.store.selectSignal(selectError);
  readonly notice = this.store.selectSignal(selectNotice);

  readonly words = outcomeWords;
  readonly notes = signal<Record<number, string>>({});

  constructor() {
    effect(() => {
      const id = Number(this.id());
      if (Number.isInteger(id) && id > 0) {
        this.store.dispatch(CodRemittanceActions.detailOpened({ id }));
      }
    });
  }

  typeNote(lineId: number, event: Event): void {
    const value = (event.target as HTMLInputElement).value;
    this.notes.update((n) => ({ ...n, [lineId]: value }));
  }

  resolve(lineId: number, event: Event): void {
    event.preventDefault();
    const note = (this.notes()[lineId] ?? '').trim();
    if (note.length > 0) {
      this.store.dispatch(CodRemittanceActions.lineResolved({ id: Number(this.id()), lineId, note }));
    }
  }
}
