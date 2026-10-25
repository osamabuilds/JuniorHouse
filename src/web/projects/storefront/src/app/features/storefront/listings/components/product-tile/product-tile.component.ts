import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ListingTile } from '../../models/listing.model';

const SIZES = '(min-width: 64rem) 25vw, (min-width: 40rem) 33vw, 50vw';

/**
 * One product in a grid (LST-12): an article with a link, a picture with WebP and JPEG sources at every generated width, explicit
 * dimensions (no layout shift) and the first row loaded eagerly. Shows only that some sizes are sold out, never a count (PCT-3).
 */
@Component({
  selector: 'rp-product-tile',
  imports: [DecimalPipe, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './product-tile.component.html',
})
export class ProductTileComponent {
  readonly tile = input.required<ListingTile>();
  /** True for the products in the first row: loaded eagerly and, for the very first, at high priority. */
  readonly eager = input(false);
  readonly priority = input(false);

  protected readonly sizes = SIZES;
  protected readonly webpSet = computed(() => this.tile().image?.sources.map((s) => `${s.webp} ${s.width}w`).join(', ') ?? '');
  protected readonly jpegSet = computed(() => {
    const sources = this.tile().image?.sources.filter((s) => s.jpeg) ?? [];
    return sources.map((s) => `${s.jpeg} ${s.width}w`).join(', ');
  });
  protected readonly fallbackSrc = computed(() => {
    const sources = this.tile().image?.sources ?? [];
    const pick = sources.find((s) => s.width >= 640) ?? sources[sources.length - 1];
    return pick?.jpeg ?? pick?.webp ?? '';
  });
  protected readonly onSale = computed(() => (this.tile().savingPercent ?? 0) > 0);
  protected readonly colours = computed(() => {
    const n = this.tile().colourCount;
    return n > 1 ? `${n} colours` : null;
  });
}
