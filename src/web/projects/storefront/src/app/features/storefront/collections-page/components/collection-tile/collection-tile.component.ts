import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { CollectionTile } from '../../models/collections-page.model';

const SIZES = '(min-width: 40rem) 50vw, 100vw';

/**
 * One collection on the index (design system: CollectionsPage; COL-1, COL-7): a single link over a 4:5 picture with a scrim, an eyebrow, the
 * name as a heading and how many pieces it holds. The picture has its alt text and explicit size; only the first row loads eagerly. A
 * collection with no picture yet shows the neutral placeholder, never a broken image.
 */
@Component({
  selector: 'rp-collection-tile',
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './collection-tile.component.html',
})
export class CollectionTileComponent {
  readonly item = input.required<CollectionTile>();
  readonly eager = input(false);

  protected readonly sizes = SIZES;
  protected readonly webpSet = computed(() => this.item().image?.sources.map((s) => `${s.webp} ${s.width}w`).join(', ') ?? '');
  protected readonly jpegSet = computed(() => (this.item().image?.sources.filter((s) => s.jpeg) ?? []).map((s) => `${s.jpeg} ${s.width}w`).join(', '));
  protected readonly fallback = computed(() => {
    const sources = this.item().image?.sources ?? [];
    const pick = sources.find((s) => s.width >= 640) ?? sources[sources.length - 1];
    return pick?.jpeg ?? pick?.webp ?? '';
  });
}
