import { CurrencyPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { ShippingLabel, barRuns } from '../../models';

/**
 * The label as it prints (LBL-1 to LBL-6): an article with the recipient, full address and phone, the courier and waybill as text and as a Code 128
 * barcode drawn in SVG, and the cash to collect printed large or "Paid". A reprint is marked. It has no heading of its own: the page has the one
 * heading. Presentational.
 */
@Component({
  selector: 'app-label-sheet',
  imports: [CurrencyPipe],
  templateUrl: './label-sheet.component.html',
  styleUrl: './label-sheet.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LabelSheetComponent {
  readonly label = input.required<ShippingLabel>();

  /** Ten quiet modules on each side, so a scanner can find the start and the stop. */
  protected readonly quiet = 10;
  protected readonly bars = computed(() => barRuns(this.label().barcodeModules));
  protected readonly modules = computed(() => this.label().barcodeModules.length);
}
