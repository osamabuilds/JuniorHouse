import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ListingTileSize } from '../../models/listing.model';

export type QuickAddState = 'choosing' | 'added';

export interface QuickAddChoice {
  readonly variantId: number;
  readonly sizeName: string;
  readonly colourName: string | null;
}

/**
 * The size bar on a product card (design system: QuickAdd; CRD-4..8). Presentational: it shows the sizes and says what happened, and the
 * container decides what pressing a size does. A sold-out size stays in the row, struck through and `aria-disabled` with "sold out" in its
 * accessible name; it is never hidden and never colour alone. The marigold is not used here: the actions are ink.
 */
@Component({
  selector: 'rp-quick-add-bar',
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './quick-add-bar.component.html',
})
export class QuickAddBarComponent {
  readonly sizes = input.required<readonly ListingTileSize[]>();
  readonly productPath = input.required<string>();
  readonly state = input<QuickAddState>('choosing');
  /** The bag is changing: nothing more can be pressed until it has answered. */
  readonly busy = input(false);
  /** What was added, in words ("Added, size 3Y, Rose"). */
  readonly added = input<string | null>(null);
  /** A refusal or failure, in words. */
  readonly notice = input<string | null>(null);

  readonly choose = output<QuickAddChoice>();
  readonly close = output<void>();
  readonly viewBag = output<void>();

  protected readonly rows = computed(() => this.sizes().map((s) => ({ size: s, buyable: this.buyable(s) })));
  protected readonly allSoldOut = computed(() => this.sizes().length > 0 && this.rows().every((r) => !r.buyable));
  /** One size, or none: Add to bag directly, no chips. */
  protected readonly single = computed(() => this.sizes().length === 1);
  protected readonly notifyQuery = computed(() => ({ size: this.sizes()[0]?.code }));

  private buyable(s: ListingTileSize): boolean {
    if (!s.variantId || s.variantId <= 0) return false;
    if (s.availability) return s.availability === 'InStock' || s.availability === 'LowStock';
    return !s.soldOut;
  }

  protected press(s: ListingTileSize, buyable: boolean): void {
    if (!buyable || this.busy() || !s.variantId) return;
    this.choose.emit({ variantId: s.variantId, sizeName: s.name, colourName: s.colourName ?? null });
  }
}
