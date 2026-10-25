import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { SizeOption } from '../../models/product-page.model';

/**
 * What the shopper is told about availability (PDP-4, 16, 17). Availability is worded as a guide, never as "reserved for you": it may be
 * a few seconds old, and the cart makes the authoritative check. Add to Cart and Notify Me arrive in later sprints (Q-PDP-1), so there is
 * no purchase control here. There is never a stock count (PCT-3).
 */
@Component({
  selector: 'rp-availability-note',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <p class="note" role="status">{{ text() }}</p>
    <p class="note note--fine">Availability is a guide and can change. It is checked again when you order.</p>
  `,
  styles: `
    .note { margin: 0; font: 400 15px/24px var(--font-sans); }
    .note--fine { margin-top: var(--space-2); font-size: 13px; line-height: 20px; color: var(--ink-muted); }
  `,
})
export class AvailabilityNoteComponent {
  /** The chosen size, or null before a choice. */
  readonly size = input<SizeOption | null>(null);
  readonly isAvailable = input.required<boolean>();

  protected readonly text = computed(() => {
    const size = this.size();
    if (size) {
      switch (size.availability) {
        case 'InStock':
          return `Size ${size.name}: in stock.`;
        case 'LowStock':
          return `Size ${size.name}: only a few left.`;
        case 'SoldOut':
          return `Size ${size.name}: sold out.`;
        default:
          return `Size ${size.name}: not yet available.`;
      }
    }
    return this.isAvailable() ? 'Choose a size to see its availability.' : 'Sold out.';
  });
}
