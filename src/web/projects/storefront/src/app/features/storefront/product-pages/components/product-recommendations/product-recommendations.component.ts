import { ChangeDetectionStrategy, Component, DestroyRef, ElementRef, afterNextRender, inject, input, output, signal, viewChild } from '@angular/core';
import { IconComponent } from '../../../../../shared/ui/icon/icon.component';
import { ProductTileComponent } from '../../../listings/components/product-tile/product-tile.component';
import { ListingTile } from '../../../listings/models/listing.model';

/**
 * One recommendation row under a product (REC-6, REC-9, REC-14), on the design's ProductRail: a <section> with an h2, the existing product cards
 * in a scroll-snap list, and Previous and Next buttons on a desktop. The list is focusable and labelled so a keyboard can scroll it, and
 * every card is the plain card with a real link, so the server HTML is complete. Nothing is rendered for an empty row (no empty heading).
 */
@Component({
  selector: 'rp-product-recommendations',
  imports: [ProductTileComponent, IconComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './product-recommendations.component.html',
  host: { '(click)': 'onClick($event)' },
})
export class ProductRecommendationsComponent {
  /** The row heading, and the name of the list. */
  readonly heading = input.required<string>();
  /** The small line above the heading. */
  readonly eyebrow = input<string | null>(null);
  /** The id of the heading, which names the section (unique on the page). */
  readonly headingId = input.required<string>();
  readonly tiles = input.required<readonly ListingTile[]>();

  /** A card's link was used: the product id. */
  readonly opened = output<number>();
  /** A size from a card was added to the bag: the product id. */
  readonly added = output<number>();

  private readonly rail = viewChild<ElementRef<HTMLElement>>('rail');
  private readonly destroyRef = inject(DestroyRef);

  /** Set in the browser from the real sizes: the arrows show only when the list is wider than its box, and each stops at its end. */
  protected readonly canPrev = signal(false);
  protected readonly canNext = signal(false);
  protected readonly overflows = signal(false);

  constructor() {
    afterNextRender(() => {
      const list = this.rail()?.nativeElement;
      if (!list) return;
      const update = () => this.measure();
      list.addEventListener('scroll', update, { passive: true });
      const observer = typeof ResizeObserver === 'function' ? new ResizeObserver(update) : null;
      observer?.observe(list);
      this.destroyRef.onDestroy(() => {
        list.removeEventListener('scroll', update);
        observer?.disconnect();
      });
      update();
    });
  }

  private measure(): void {
    const list = this.rail()?.nativeElement;
    if (!list) return;
    const max = list.scrollWidth - list.clientWidth;
    this.overflows.set(max > 1);
    this.canPrev.set(list.scrollLeft > 1);
    this.canNext.set(list.scrollLeft < max - 1);
  }

  /** Scrolls by one card (its width and the gap), smoothly unless the visitor prefers less motion. */
  protected scroll(direction: 1 | -1): void {
    const list = this.rail()?.nativeElement;
    const first = list?.querySelector<HTMLElement>('li');
    if (!list || !first) return;
    const gap = parseFloat(getComputedStyle(list).columnGap) || 0;
    const reduce = typeof matchMedia === 'function' && matchMedia('(prefers-reduced-motion: reduce)').matches;
    list.scrollBy({ left: direction * (first.getBoundingClientRect().width + gap), behavior: reduce ? 'auto' : 'smooth' });
  }

  /** One listener for the whole row: a click that lands on a card's link counts as opening that product. */
  protected onClick(event: Event): void {
    const link = (event.target as Element | null)?.closest?.('li a[href]');
    const item = link?.closest('li');
    const id = item ? Number(item.getAttribute('data-product-id')) : NaN;
    if (Number.isFinite(id) && id > 0) this.opened.emit(id);
  }
}
