import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, ElementRef, afterNextRender, computed, inject, input, output, signal, viewChild } from '@angular/core';
import { RouterLink } from '@angular/router';
import { WishlistHeartComponent } from '../../../../customers/wishlist/components/wishlist-heart/wishlist-heart.component';
import { imageInView } from '../../../../../shared/utils/zoom.util';
import { watchMedia } from '../../../../../shared/utils/viewport';
import { QuickAddContainer } from '../../containers/quick-add/quick-add.container';
import { ListingImage, ListingTile } from '../../models/listing.model';
import { cardTag } from '../../utils/card-tag.util';

const SIZES = '(min-width: 64rem) 25vw, (min-width: 40rem) 33vw, 50vw';

interface PictureView {
  readonly alt: string;
  readonly width: number;
  readonly height: number;
  readonly webpSet: string;
  readonly jpegSet: string;
  readonly fallback: string;
}

function view(image: ListingImage | null | undefined): PictureView | null {
  if (!image || image.sources.length === 0) return null;
  const jpegs = image.sources.filter((s) => s.jpeg);
  const pick = image.sources.find((s) => s.width >= 640) ?? image.sources[image.sources.length - 1];
  return {
    alt: image.altText,
    width: image.width,
    height: image.height,
    webpSet: image.sources.map((s) => `${s.webp} ${s.width}w`).join(', '),
    jpegSet: jpegs.map((s) => `${s.jpeg} ${s.width}w`).join(', '),
    fallback: pick?.jpeg ?? pick?.webp ?? '',
  };
}

/**
 * One product in a grid (LST-12; CRD-1..3, 6, 13). An article with a link, a picture with WebP and JPEG sources at every generated width,
 * explicit dimensions (no layout shift) and the first row loaded eagerly. Shows only that some sizes are sold out, never a count (PCT-3).
 * With scripting it adds what the design asks of a card: a second picture (requested on the first hover or focus on a desktop, a swipe with
 * two dots on touch) and the quick-add bar, opened by hover or focus on a desktop and by a round + on touch. The server HTML is the plain
 * card, so a visitor without scripting still sees the picture, name, price and sizes and can open the product.
 */
@Component({
  selector: 'rp-product-tile',
  imports: [DecimalPipe, RouterLink, WishlistHeartComponent, QuickAddContainer],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './product-tile.component.html',
  host: { '(pointerenter)': 'entered()', '(focusin)': 'wantSecond()', '(keydown.escape)': 'dismiss()' },
})
export class ProductTileComponent {
  private readonly destroyRef = inject(DestroyRef);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef).nativeElement;
  private readonly strip = viewChild<ElementRef<HTMLElement>>('strip');

  readonly tile = input.required<ListingTile>();
  /** True for the products in the first row: loaded eagerly and, for the very first, at high priority. */
  readonly eager = input(false);
  readonly priority = input(false);
  /** The server refused a quick add for a size this card still showed as available: the page should load the listing again. */
  readonly stale = output<void>();

  protected readonly sizes = SIZES;
  protected readonly main = computed(() => view(this.tile().image));
  protected readonly tag = computed(() => cardTag(this.tile()));
  protected readonly onSale = computed(() => (this.tile().savingPercent ?? 0) > 0);
  protected readonly colours = computed(() => {
    const n = this.tile().colourCount;
    return n > 1 ? `${n} colours` : null;
  });

  /** Set by the browser after hydration, so the server HTML is the plain card. */
  protected readonly enhanced = signal(false);
  private readonly touch = signal(false);
  private readonly secondWanted = signal(false);
  protected readonly open = signal(false);
  /** Escape put the bar away until the pointer or focus comes back to it. */
  protected readonly dismissed = signal(false);
  protected readonly slide = signal(0);

  /** The second picture exists only once it is wanted: on first hover or focus on a desktop, straight away on touch (never at page load). */
  protected readonly second = computed(() => (this.secondWanted() || (this.enhanced() && this.touch()) ? view(this.tile().secondImage) : null));
  protected readonly canQuickAdd = computed(() => this.tile().sizes.some((s) => (s.variantId ?? 0) > 0));

  constructor() {
    afterNextRender(() => {
      this.enhanced.set(true);
      const stop = watchMedia('(max-width: 63.99rem)', (matches) => this.touch.set(matches));
      this.destroyRef.onDestroy(stop);
    });
  }

  protected entered(): void {
    this.dismissed.set(false);
    this.wantSecond();
  }

  /** Escape: close the bar and return focus to the card's own link (CRD-11). */
  protected dismiss(): void {
    if (!this.enhanced()) return;
    this.open.set(false);
    this.dismissed.set(true);
    this.host.querySelector<HTMLElement>('h3 a')?.focus();
  }

  protected barFocused(): void {
    this.dismissed.set(false);
  }

  protected wantSecond(): void {
    if (this.tile().secondImage) this.secondWanted.set(true);
  }

  protected toggle(): void {
    this.open.update((v) => !v);
  }

  protected close(): void {
    this.open.set(false);
  }

  /** The two dots follow the swipe strip on touch. */
  protected onScroll(): void {
    const el = this.strip()?.nativeElement;
    if (el) this.slide.set(imageInView(el.scrollLeft, el.clientWidth, 2));
  }
}
