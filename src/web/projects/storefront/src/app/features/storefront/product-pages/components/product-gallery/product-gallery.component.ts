import { ChangeDetectionStrategy, Component, ElementRef, OnDestroy, computed, effect, input, signal, viewChild } from '@angular/core';
import { LightboxComponent, LightboxImage } from '../../../../../shared/ui/lightbox/lightbox.component';
import { imageInView } from '../../../../../shared/utils/zoom.util';
import { ProductImage } from '../../models/product-page.model';

const SIZES = '(min-width: 64rem) 28vw, 100vw';
const SETTLE_MS = 120;

/**
 * The product pictures (PDP-1, 10, 11; GAL-1, 2, 3, 9, 11). One list serves every screen: a snap-scrolling strip on a phone, a two-column
 * grid on a desktop, so nothing needs scripting and every picture is in the server HTML. Each picture is a button that opens the viewer
 * (an enhancement: without scripting the button does nothing and the pictures are still all there). The first picture is the LCP image:
 * eager, high priority. The rest are lazy. Every picture has its alt text and explicit width and height, so nothing shifts.
 */
@Component({
  selector: 'rp-product-gallery',
  imports: [LightboxComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './product-gallery.component.html',
})
export class ProductGalleryComponent implements OnDestroy {
  readonly images = input.required<readonly ProductImage[]>();

  private readonly list = viewChild<ElementRef<HTMLElement>>('list');
  private readonly viewer = viewChild(LightboxComponent);
  private timer: ReturnType<typeof setTimeout> | undefined;

  protected readonly sizes = SIZES;
  /** The picture most in view on the phone strip, for the counter. */
  protected readonly current = signal(0);

  protected readonly items = computed(() =>
    this.images().map((image) => {
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
    }),
  );

  /** What the viewer shows: the largest derivative that exists for each picture (Q-GAL-1), asked for only when it opens. */
  protected readonly viewerImages = computed((): readonly LightboxImage[] =>
    this.images().map((image) => {
      const largest = [...image.sources].sort((a, b) => b.width - a.width)[0];
      return { alt: image.altText, width: image.width, height: image.height, src: largest?.webp ?? '', fallback: largest?.jpeg ?? null };
    }),
  );

  constructor() {
    // A different colour brings different pictures: start again at the first.
    effect(() => {
      this.images();
      this.current.set(0);
    });
  }

  protected open(index: number): void {
    this.viewer()?.open(index);
  }

  /** Update the counter when the strip stops moving, so a screen reader hears one change, not one per frame. */
  protected onScroll(): void {
    clearTimeout(this.timer);
    this.timer = setTimeout(() => {
      const el = this.list()?.nativeElement;
      if (el) this.current.set(imageInView(el.scrollLeft, el.clientWidth, this.images().length));
    }, SETTLE_MS);
  }

  ngOnDestroy(): void {
    clearTimeout(this.timer);
  }
}
