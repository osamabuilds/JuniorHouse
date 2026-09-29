import { ChangeDetectionStrategy, Component, effect, inject, input, untracked } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Store } from '@ngrx/store';
import { LookupNames } from '@features/reference-data';
import { ActingPersonService, AppDatePipe } from '@shared';
import { GrnFormComponent } from '../../components/grn-form/grn-form.component';
import { ReconciliationBadgeComponent } from '../../components/reconciliation-badge/reconciliation-badge.component';
import { RecordReceiptValue } from '../../models';
import {
  ReceivePageActions,
  selectDelivery,
  selectDeliveryError,
  selectDeliveryLoading,
  selectReceipt,
  selectReceiptError,
  selectReceiptSaving,
} from '../../store';

/**
 * Sprint 4: counting one delivery against its delivery note (BRD 5.11, spec AC-3..AC-12). Once the goods receipt is recorded the page
 * shows the GRN number and how the count compared with the vendor's claim, and points on to the QC batch it opened.
 */
@Component({
  selector: 'app-receive-page',
  imports: [RouterLink, AppDatePipe, GrnFormComponent, ReconciliationBadgeComponent],
  template: `
    <p><a routerLink="/quality">&larr; Receiving queue</a></p>

    @if (loadError(); as error) {
      <h1>Receive goods</h1>
      <p class="banner banner--error" role="alert">
        {{ error.status === 404 ? 'This delivery note could not be found.' : error.message }}
      </p>
    } @else if (receipt(); as grn) {
      <h1>Goods received: {{ grn.grnNo }}</h1>
      <p class="banner banner--success" role="status">
        The goods receipt is recorded and a QC batch is waiting to be inspected.
      </p>

      <section class="panel" aria-labelledby="result-heading">
        <h2 id="result-heading">How the count compared</h2>
        <dl class="facts">
          <div>
            <dt>Purchase order</dt>
            <dd>{{ grn.poNo }}</dd>
          </div>
          <div>
            <dt>Delivery note</dt>
            <dd>{{ grn.vendorDnNo }}</dd>
          </div>
          <div>
            <dt>Received</dt>
            <dd>{{ grn.receivedDate | appDate }} by {{ grn.actingPerson }}</dd>
          </div>
          <div>
            <dt>Result</dt>
            <dd><app-reconciliation-badge [code]="grn.reconciliationCode" [diff]="grn.diffTotal" /></dd>
          </div>
        </dl>

        <table class="responsive">
          <caption class="sr-only">
            Claimed and counted quantities
          </caption>
          <thead>
            <tr>
              <th scope="col">Size</th>
              <th scope="col">Colour</th>
              <th scope="col">Vendor said</th>
              <th scope="col">Counted</th>
              <th scope="col">Difference</th>
            </tr>
          </thead>
          <tbody>
            @for (line of grn.lines; track line.sizeId + '-' + line.colourId) {
              <tr>
                <td data-label="Size">{{ names.sizeName(line.sizeId) }}</td>
                <td data-label="Colour">{{ names.colourName(line.colourId) }}</td>
                <td data-label="Vendor said">{{ line.claimedQty }}</td>
                <td data-label="Counted">{{ line.countedQty }}</td>
                <td data-label="Difference">
                  @if (line.diffQty === 0) {
                    Matches
                  } @else if (line.diffQty < 0) {
                    Short by {{ -line.diffQty }}
                  } @else {
                    Over by {{ line.diffQty }}{{ line.isExcess ? ' (extra to inspect)' : '' }}
                  }
                </td>
              </tr>
            }
          </tbody>
        </table>

        <p>
          <a class="button button--primary" routerLink="/quality/batches" [queryParams]="{ search: grn.grnNo }">Go to the QC batch</a>
          <a class="button" routerLink="/quality">Back to the queue</a>
        </p>
      </section>
    } @else if (delivery(); as delivery) {
      <h1>Receive goods: {{ delivery.vendorDnNo }}</h1>
      <p class="page-intro">
        Purchase order {{ delivery.poNo }}, dispatched {{ delivery.dispatchDate | appDate }}. The vendor says
        {{ delivery.claimedTotal }} pieces are on this delivery note.
      </p>

      @if (delivery.isCancelled) {
        <p class="banner banner--error" role="note">This purchase order was cancelled, so no goods receipt can be recorded for it.</p>
      } @else if (delivery.grnNo) {
        <p class="banner banner--info" role="note">
          This delivery note was already received as {{ delivery.grnNo }}; only one goods receipt is recorded per delivery note.
        </p>
      } @else {
        <app-grn-form [delivery]="delivery" [names]="names" [saving]="saving()" [error]="saveError()" [defaultPerson]="defaultPerson()" (submitted)="record($event)" />
      }
    } @else if (loading()) {
      <h1>Receive goods</h1>
      <p class="state-message">Loading…</p>
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ReceivePageContainer {
  private readonly store = inject(Store);
  private readonly actingPerson = inject(ActingPersonService);
  readonly names = inject(LookupNames);

  /** The inbound delivery's id from the route (withComponentInputBinding). */
  readonly id = input.required<string>();

  readonly delivery = this.store.selectSignal(selectDelivery);
  readonly loading = this.store.selectSignal(selectDeliveryLoading);
  readonly loadError = this.store.selectSignal(selectDeliveryError);
  readonly saving = this.store.selectSignal(selectReceiptSaving);
  readonly saveError = this.store.selectSignal(selectReceiptError);
  readonly receipt = this.store.selectSignal(selectReceipt);
  readonly defaultPerson = this.actingPerson.name;

  constructor() {
    effect(() => {
      const id = Number(this.id());
      untracked(() => this.store.dispatch(ReceivePageActions.opened({ id })));
    });
  }

  record(value: RecordReceiptValue): void {
    this.actingPerson.remember(value.actingPerson);
    this.store.dispatch(ReceivePageActions.receiptSubmitted({ value }));
  }
}
