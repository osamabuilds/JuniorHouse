import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { LabelWorkItemDto } from '../../models';
import { PARK_REASON_TEXT } from '../../label-work.constants';

/**
 * The accepted batches awaiting tags (LW-7): one row per accepted line with what is printed so far, what is waiting for attention,
 * and the actions that apply. Presentational only: it shows what it is given and emits what the user chose.
 */
@Component({
  selector: 'app-worklist-table',
  template: `
    @if (loading()) {
      <p class="state-message">Loading…</p>
    } @else if (items().length === 0) {
      <p class="state-message">Nothing is waiting for tags.</p>
    } @else {
      <table class="responsive">
        <caption class="sr-only">Accepted batches awaiting tags</caption>
        <thead>
          <tr>
            <th scope="col">SKU</th>
            <th scope="col">Style</th>
            <th scope="col">Colour / size</th>
            <th scope="col">Accepted</th>
            <th scope="col">Tags</th>
            <th scope="col">Status</th>
            <th scope="col">Actions</th>
          </tr>
        </thead>
        <tbody>
          @for (item of items(); track item.id) {
            <tr>
              <td data-label="SKU">
                @if (item.skuCode) {
                  <strong>{{ item.skuCode }}</strong><br />
                  <small>{{ item.gtin }}</small>
                } @else {
                  <span class="badge badge--muted">No SKU yet</span>
                }
              </td>
              <td data-label="Style">
                {{ item.styleName ?? 'Unknown style' }}
                @if (item.styleCode) {
                  <br /><small>{{ item.styleCode }}</small>
                }
                @if (!item.styleIsActive) {
                  <span class="badge badge--muted">Retired style</span>
                }
              </td>
              <td data-label="Colour / size">{{ item.colourName }} / {{ item.sizeName }}</td>
              <td data-label="Accepted">
                {{ item.qty }}
                @if (item.outcomeCode === 'Concession') {
                  <span class="badge">Concession (2ND)</span>
                }
              </td>
              <td data-label="Tags">
                {{ item.printedQty }} printed
                @if (item.activeQty > item.printedQty) {
                  <br /><small>{{ item.activeQty - item.printedQty }} queued</small>
                }
                <br /><small>{{ item.remainingQty }} still allowed</small>
              </td>
              <td data-label="Status">
                @if (item.statusCode === 'Ready') {
                  <span class="badge">Ready to print</span>
                } @else {
                  <span class="badge badge--muted">Needs attention</span>
                  <br /><small>{{ parkText(item) }}</small>
                }
                @if (item.priceChanged) {
                  <br /><small class="qc-flag qc-flag--warn">Price changed since these tags were queued. Reprint to put the new price on the tag.</small>
                }
              </td>
              <td data-label="Actions">
                @if (item.statusCode === 'Ready') {
                  <button type="button" class="button button--small button--primary" [disabled]="item.remainingQty === 0" (click)="queue.emit(item)">
                    Queue tags
                  </button>
                  <a class="button button--small" [href]="previewUrlFor()(item.id)" target="_blank" rel="noopener">Preview tag</a>
                } @else if (item.parkReasonCode === 'StyleRetired') {
                  <button type="button" class="button button--small" [disabled]="resolvingId() === item.id" (click)="confirmRetired.emit(item)">
                    Label anyway
                  </button>
                } @else {
                  <button type="button" class="button button--small" [disabled]="resolvingId() === item.id" (click)="resolve.emit(item)">
                    {{ resolvingId() === item.id ? 'Checking…' : 'Try again' }}
                  </button>
                }
              </td>
            </tr>
          }
        </tbody>
      </table>
    }
  `,
  styles: ':host { display: block; } small { color: var(--color-muted, #555); }',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class WorklistTableComponent {
  readonly items = input.required<readonly LabelWorkItemDto[]>();
  readonly loading = input.required<boolean>();
  readonly resolvingId = input<number | null>(null);
  /** Where a tag's PDF preview is served; supplied by the container so this component stays free of services. */
  readonly previewUrlFor = input.required<(id: number) => string>();

  readonly queue = output<LabelWorkItemDto>();
  readonly resolve = output<LabelWorkItemDto>();
  readonly confirmRetired = output<LabelWorkItemDto>();

  parkText(item: LabelWorkItemDto): string {
    return (item.parkReasonCode && PARK_REASON_TEXT[item.parkReasonCode]) || 'Something needs to be fixed first.';
  }
}
