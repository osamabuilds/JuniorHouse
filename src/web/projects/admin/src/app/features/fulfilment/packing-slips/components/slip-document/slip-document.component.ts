import { CurrencyPipe, DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { PackingSlip } from '../../models';

/**
 * The slip as it prints (SLP-1 to SLP-5, SLP-7, SLP-8): a semantic article with the items, prices and totals, the cash to collect printed large for
 * cash on delivery or "Paid" for an online order, how to exchange or return, and who to ask. A reprint is marked. It has no heading of its own: the
 * page it sits in has the one heading. Presentational.
 */
@Component({
  selector: 'app-slip-document',
  imports: [CurrencyPipe, DatePipe],
  templateUrl: './slip-document.component.html',
  styleUrl: './slip-document.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SlipDocumentComponent {
  readonly slip = input.required<PackingSlip>();

  /** The WhatsApp number as shown: digits from the settings with a plus in front. */
  whatsapp(value: string): string {
    return value.startsWith('+') ? value : `+${value}`;
  }
}
