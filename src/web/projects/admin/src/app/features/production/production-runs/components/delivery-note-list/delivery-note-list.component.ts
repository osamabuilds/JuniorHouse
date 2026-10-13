import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { LookupNameResolver } from '@features/reference/lookups';
import { AppDatePipe } from '@shared';
import { DeliveryNoteDto, OrderedVsShippedDto } from '../../models';

/**
 * Delivery note history and ordered-versus-shipped (spec AC-19, AC-21, AC-22, AC-24, AC-37, AC-33). Every warning is
 * words: "Over by 5 pieces", "Short by 23 pieces", "Late by 2 days", "Romp fabric delay". Nothing relies on colour alone.
 */
@Component({
  selector: 'app-delivery-note-list',
  imports: [AppDatePipe],
  template: `
    @if (quantities().length > 0) {
      <h3>Ordered and shipped so far</h3>
      <table class="responsive">
        <caption class="sr-only">
          Ordered and shipped so far
        </caption>
        <thead>
          <tr>
            <th scope="col">Size</th>
            <th scope="col">Colour</th>
            <th scope="col">Ordered</th>
            <th scope="col">Shipped</th>
            <th scope="col">Still needed</th>
          </tr>
        </thead>
        <tbody>
          @for (row of quantities(); track row.sizeId + '-' + row.colourId) {
            <tr>
              <td data-label="Size">{{ names().sizeName(row.sizeId) }}</td>
              <td data-label="Colour">{{ names().colourName(row.colourId) }}</td>
              <td data-label="Ordered">{{ row.ordered }}</td>
              <td data-label="Shipped">
                {{ row.shipped }}
                @if (row.shipped > row.maxAllowed) {
                  <span class="flag flag--danger">Over the limit of {{ row.maxAllowed }}</span>
                }
              </td>
              <td data-label="Still needed">{{ stillNeeded(row) }}</td>
            </tr>
          }
        </tbody>
      </table>
    }

    <h3>Delivery notes</h3>
    @if (notes().length === 0) {
      <p class="state-message state-message--inline">No delivery notes yet.</p>
    } @else {
      @for (note of notes(); track note.id) {
        <article class="note" [attr.aria-labelledby]="'dn-' + note.id">
          <h4 id="dn-{{ note.id }}">
            Delivery note {{ note.vendorDnNo }}
            @if (note.isFinalShipment) {
              <span class="badge badge--muted">Final shipment</span>
            }
          </h4>
          <p class="note__meta">
            Dispatched {{ note.dispatchDate | appDate }} &middot; recorded {{ note.recordedAt | appDate: 'datetime' }} by {{ note.actingPerson }} &middot;
            judged against revision {{ note.revisionNumber }} of the order
          </p>
          <ul class="note__flags">
            @if (note.timingCode === 'Late') {
              <li><span class="flag flag--warn">Late by {{ note.daysLate }} {{ note.daysLate === 1 ? 'day' : 'days' }}</span></li>
            } @else if (note.timingCode === 'BeyondAcceptable') {
              <li>
                <span class="flag flag--danger">
                  Beyond the latest acceptable date ({{ note.daysLate }} {{ note.daysLate === 1 ? 'day' : 'days' }} late)
                </span>
              </li>
            } @else {
              <li><span class="flag">On time</span></li>
            }
            @if (note.quantityStatusCode === 'Over') {
              <li>
                <span class="flag flag--danger">Over by {{ note.overQty }} {{ note.overQty === 1 ? 'piece' : 'pieces' }}</span>
                <span class="note__hint">Recorded as sent. When the goods arrive, the receiving check decides what happens to the extra.</span>
              </li>
            }
            @if (note.quantityStatusCode === 'Short') {
              <li><span class="flag flag--danger">Short by {{ note.shortQty }} {{ note.shortQty === 1 ? 'piece' : 'pieces' }}</span></li>
            }
            @if (note.isRompFabricDelay) {
              <li>
                <span class="flag flag--warn">Romp fabric delay</span>
                <span class="note__hint">
                  {{ note.fabricDispatchDate ? 'Romp sent the fabric on ' + (note.fabricDispatchDate | appDate) + ', after cutting started.' : 'Romp never recorded sending the fabric.' }}
                  This is not counted against the vendor.
                </span>
              </li>
            }
          </ul>
          <table class="line-grid">
            <caption class="sr-only">
              Pieces on delivery note {{ note.vendorDnNo }}
            </caption>
            <thead>
              <tr>
                <th scope="col">Size</th>
                <th scope="col">Colour</th>
                <th scope="col">Pieces</th>
              </tr>
            </thead>
            <tbody>
              @for (line of note.lines; track line.sizeId + '-' + line.colourId) {
                <tr>
                  <td>{{ names().sizeName(line.sizeId) }}</td>
                  <td>{{ names().colourName(line.colourId) }}</td>
                  <td>{{ line.qty }}</td>
                </tr>
              }
            </tbody>
          </table>
        </article>
      }
    }
  `,
  styles: `
    :host {
      display: block;
    }
    .note {
      border: 1px solid var(--color-border);
      border-radius: var(--radius-md);
      padding: var(--space-4);
      margin-bottom: var(--space-4);
      background: var(--color-surface);
    }
    .note h4 {
      margin: 0 0 var(--space-2);
      display: flex;
      flex-wrap: wrap;
      gap: var(--space-2);
      align-items: center;
    }
    .note__meta {
      margin: 0 0 var(--space-2);
      color: var(--color-ink-soft);
      font-size: var(--text-sm);
    }
    .note__flags {
      list-style: none;
      margin: 0 0 var(--space-3);
      padding: 0;
      display: grid;
      gap: var(--space-2);
    }
    .note__hint {
      display: block;
      margin-top: var(--space-1);
      color: var(--color-ink-soft);
      font-size: var(--text-xs);
    }
    .flag {
      display: inline-block;
      padding: 0.1875rem var(--space-2);
      border-radius: var(--radius-sm);
      font-size: var(--text-xs);
      font-weight: 600;
      border: 1px solid var(--color-border-strong);
      background: var(--color-surface);
    }
    .flag--danger {
      background: var(--color-danger-tint);
      color: var(--color-danger);
      border-color: var(--color-danger);
    }
    .flag--warn {
      background: var(--color-brass-tint);
      color: var(--color-brass-dark);
      border-color: var(--color-brass);
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DeliveryNoteListComponent {
  readonly notes = input.required<readonly DeliveryNoteDto[]>();
  readonly quantities = input.required<readonly OrderedVsShippedDto[]>();
  readonly names = input.required<LookupNameResolver>();

  /** Pieces still to come to reach the minimum the terms allow (never negative). */
  stillNeeded(row: OrderedVsShippedDto): number {
    return Math.max(0, row.minRequired - row.shipped);
  }
}
