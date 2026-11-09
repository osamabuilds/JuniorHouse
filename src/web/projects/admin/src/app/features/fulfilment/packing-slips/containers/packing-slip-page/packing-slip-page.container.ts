import { ChangeDetectionStrategy, Component, effect, inject, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Store } from '@ngrx/store';
import { SlipDocumentComponent } from '../../components/slip-document/slip-document.component';
import { PackingSlipPageActions, selectError, selectLoading, selectNotice, selectPrinting, selectSlip } from '../../store';

/**
 * The packing slip (docs/specs/fulfilment/packing-slips, SLP-1 to SLP-8): the slip that goes inside the parcel. "Send to the printer" goes through
 * the printer port and never blocks the pack; "Print this page" uses the browser. The buttons are left out of the printed page. The page has one
 * heading, and the slip is built from Orders at the moment it is opened.
 */
@Component({
  selector: 'app-packing-slip-page',
  imports: [RouterLink, SlipDocumentComponent],
  templateUrl: './packing-slip-page.container.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PackingSlipPageContainer {
  private readonly store = inject(Store);

  /** From the address (`/fulfilment/slip/:id`), bound by the router. */
  readonly id = input.required<string>();

  readonly slip = this.store.selectSignal(selectSlip);
  readonly loading = this.store.selectSignal(selectLoading);
  readonly printing = this.store.selectSignal(selectPrinting);
  readonly error = this.store.selectSignal(selectError);
  readonly notice = this.store.selectSignal(selectNotice);

  constructor() {
    effect(() => {
      const orderId = Number(this.id());
      if (Number.isInteger(orderId) && orderId > 0) {
        this.store.dispatch(PackingSlipPageActions.opened({ orderId }));
      }
    });
  }

  sendToPrinter(): void {
    this.store.dispatch(PackingSlipPageActions.printClicked({ orderId: Number(this.id()) }));
  }

  printPage(): void {
    window.print();
  }

  dismissNotice(): void {
    this.store.dispatch(PackingSlipPageActions.noticeDismissed());
  }
}
