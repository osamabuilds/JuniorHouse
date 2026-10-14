import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { RouterLink } from '@angular/router';
import { VariantRowDto } from '../../models';

/**
 * The SKU register (VAR-13): one row per variant with its readable SKU and the barcode number in use. Reprint and print history
 * are links to the Print queue screen, which owns them; the void action is emitted to the page. Presentational only.
 */
@Component({
  selector: 'app-variant-table',
  imports: [RouterLink],
  template: `
    @if (loading()) {
      <p class="state-message">Loading…</p>
    } @else if (variants().length === 0) {
      <p class="state-message">No SKUs found.</p>
    } @else {
      <table class="responsive">
        <caption class="sr-only">SKU register</caption>
        <thead>
          <tr>
            <th scope="col">SKU</th>
            <th scope="col">Barcode (GTIN-13)</th>
            <th scope="col">Style</th>
            <th scope="col">Colour / size</th>
            <th scope="col">Actions</th>
          </tr>
        </thead>
        <tbody>
          @for (variant of variants(); track variant.id) {
            <tr>
              <td data-label="SKU"><strong>{{ variant.skuCode }}</strong></td>
              <td data-label="Barcode (GTIN-13)">
                @if (variant.activeGtin) {
                  {{ variant.activeGtin }}
                } @else {
                  <span class="badge badge--muted">Voided: none in use</span>
                }
                @if (variant.retiredCount > 0) {
                  <br /><small>{{ variant.retiredCount }} retired number{{ variant.retiredCount === 1 ? '' : 's' }} on record</small>
                }
              </td>
              <td data-label="Style">{{ variant.styleName }}<br /><small>{{ variant.styleCode }}</small></td>
              <td data-label="Colour / size">{{ variant.colourName }} / {{ variant.sizeName }}</td>
              <td data-label="Actions">
                <button type="button" class="button button--small" (click)="details.emit(variant)">Barcode history</button>
                <a
                  class="button button--small"
                  routerLink="/labelling/queue"
                  [queryParams]="{ variantId: variant.id, sku: variant.skuCode }"
                >Print history</a>
                @if (variant.activeGtin) {
                  <a
                    class="button button--small"
                    routerLink="/labelling/queue"
                    [queryParams]="{ variantId: variant.id, reprint: 1, sku: variant.skuCode }"
                  >Reprint</a>
                  <button type="button" class="button button--small button--danger" (click)="voidRequested.emit(variant)">Void barcode</button>
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
export class VariantTableComponent {
  readonly variants = input.required<readonly VariantRowDto[]>();
  readonly loading = input.required<boolean>();

  readonly details = output<VariantRowDto>();
  readonly voidRequested = output<VariantRowDto>();
}
