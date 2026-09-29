import { ChangeDetectionStrategy, Component, OnInit, inject, input } from '@angular/core';
import { Store } from '@ngrx/store';
import { LookupNames } from '@features/reference-data';
import { AppDatePipe } from '@shared';
import { FILE_CATEGORY_LABELS, INITIATOR_LABELS, PO_STATUS_LABELS } from '../../purchase-orders.constants';
import { VendorPoViewActions, selectVendorView, selectVendorViewError, selectVendorViewTotal } from '../../store';

/**
 * SCRUM-93 task 51 (AC-35, AC-36): what a vendor is shown of a PO - read-only, phone-readable and
 * printable on A4, with no admin navigation. It renders only the vendor-safe view model, which has
 * no internal note, cost or evidence fields to leak. Route: /purchase-orders/:id/vendor-view (the
 * route id is bound to the `id` input).
 */
@Component({
  selector: 'app-vendor-view-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [AppDatePipe],
  template: `
    <article class="vv">
      @if (error()) {
        <h1>Purchase order</h1>
        <p class="banner banner--error" role="alert">
          {{ error()?.status === 404 ? 'This purchase order is not available.' : error()?.message }}
        </p>
      } @else if (view(); as po) {
        <header class="vv__header">
          <h1>Purchase order {{ po.poNo }}</h1>
          <p>
            For <strong>{{ po.vendorName }}</strong> &middot; {{ statusLabel(po.statusId) }} &middot; Revision
            {{ po.revisionNumber }}
          </p>
        </header>

        @if (po.pendingRevision; as pending) {
          <aside class="banner banner--error vv__pending" aria-label="Proposed change">
            <strong>A change is proposed and not yet agreed</strong> (Revision {{ pending.revisionNumber }}, from
            {{ initiatorLabel(pending.initiatorId) }}): PKR {{ pending.unitCost }} per piece, delivery {{ pending.expectedDeliveryDate | appDate }}.
            @if (pending.vendorMessage) { <span> &ldquo;{{ pending.vendorMessage }}&rdquo;</span> }
            The terms below stay in force until it is agreed.
          </aside>
        }

        <section aria-labelledby="vv-terms">
          <h2 id="vv-terms">Agreed terms</h2>
          <dl class="vv__facts">
            <div><dt>Unit cost</dt><dd>PKR {{ po.unitCost }}</dd></div>
            <div><dt>Expected delivery</dt><dd>{{ po.expectedDeliveryDate | appDate }}</dd></div>
            <div><dt>Latest acceptable delivery</dt><dd>{{ po.latestAcceptableDate | appDate }}</dd></div>
            <div><dt>Extra pieces allowed</dt><dd>{{ po.overTolerancePercent ?? '—' }}{{ po.overTolerancePercent === null ? '' : '%' }}</dd></div>
            <div><dt>Fewer pieces allowed</dt><dd>{{ po.underTolerancePercent ?? '—' }}{{ po.underTolerancePercent === null ? '' : '%' }}</dd></div>
            <div><dt>Payment terms</dt><dd>{{ names.paymentTermName(po.paymentTermId) }}</dd></div>
            <div><dt>Advance payment</dt><dd>{{ po.advancePercent }}%</dd></div>
            <div><dt>Who supplies the fabric</dt><dd>{{ names.fabricName(po.fabricResponsibilityId) }}</dd></div>
          </dl>
        </section>

        <section aria-labelledby="vv-lines">
          <h2 id="vv-lines">Quantities (total {{ total() }} pcs)</h2>
          <table>
            <caption class="sr-only">Size and colour quantities</caption>
            <thead>
              <tr>
                <th scope="col">Size</th>
                <th scope="col">Colour</th>
                <th scope="col">Quantity</th>
              </tr>
            </thead>
            <tbody>
              @for (line of po.lines; track line.sizeId + '-' + line.colourId) {
                <tr>
                  <td>{{ names.sizeName(line.sizeId) }}</td>
                  <td>{{ names.colourName(line.colourId) }}</td>
                  <td>{{ line.qty }}</td>
                </tr>
              }
            </tbody>
          </table>
        </section>

        <section aria-labelledby="vv-files">
          <h2 id="vv-files">Specification files</h2>
          @if (po.files.length === 0) {
            <p>No files attached.</p>
          } @else {
            <ul>
              @for (file of po.files; track file.id) {
                <li>{{ file.fileName }} <span class="vv__cat">({{ categoryLabel(file.categoryId) }})</span></li>
              }
            </ul>
          }
        </section>
      }
    </article>
  `,
  styles: `
    .vv {
      max-width: 720px;
      margin: 0 auto;
      padding: var(--space-4);
    }
    .vv__facts {
      display: grid;
      gap: var(--space-2);
      margin: 0 0 var(--space-5);
    }
    .vv__facts div {
      display: flex;
      justify-content: space-between;
      border-bottom: 1px solid var(--color-border);
      padding-bottom: var(--space-1);
      gap: var(--space-3);
    }
    .vv__facts dt {
      color: var(--color-ink-soft);
    }
    .vv__facts dd {
      margin: 0;
      font-weight: 500;
      text-align: right;
    }
    .vv__cat {
      color: var(--color-ink-soft);
    }
    section {
      margin-bottom: var(--space-5);
    }
    @media print {
      @page {
        size: A4;
        margin: 15mm;
      }
      .vv {
        max-width: none;
        padding: 0;
      }
      .banner {
        border: 1px solid #000;
        background: none;
        color: #000;
      }
    }
  `,
})
export class VendorViewPageContainer implements OnInit {
  private readonly store = inject(Store);
  readonly names = inject(LookupNames);

  /** Bound from the route's `:id` (withComponentInputBinding). */
  readonly id = input.required<string>();

  readonly view = this.store.selectSignal(selectVendorView);
  readonly error = this.store.selectSignal(selectVendorViewError);
  readonly total = this.store.selectSignal(selectVendorViewTotal);

  ngOnInit(): void {
    this.store.dispatch(VendorPoViewActions.opened({ poId: +this.id() }));
  }

  statusLabel(statusId: number): string {
    return PO_STATUS_LABELS[statusId] ?? `#${statusId}`;
  }

  initiatorLabel(initiatorId: number): string {
    return INITIATOR_LABELS[initiatorId] ?? 'a party';
  }

  categoryLabel(categoryId: number): string {
    return FILE_CATEGORY_LABELS[categoryId] ?? '';
  }
}
