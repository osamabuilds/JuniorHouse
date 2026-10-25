import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { ProductImage } from '../../models/product-page.model';

const SIZES = '(min-width: 64rem) 28vw, 100vw';

/**
 * The product pictures (PDP-1, 10, 11). One list serves every screen: a swipeable strip on a phone, a two-column grid on a desktop, so
 * nothing needs scripting and every picture is in the server HTML. The first picture is the LCP image: eager, high priority. The rest are
 * lazy. Every picture has its alt text and explicit width and height, so nothing shifts while it loads.
 */
@Component({
  selector: 'rp-product-gallery',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="gallery" aria-label="Product images">
      @if (images().length > 0) {
        <ul class="gallery__list" tabindex="0" aria-label="Images, scroll sideways for more">
          @for (image of items(); track $index) {
            <li class="gallery__item">
              <figure>
                <picture>
                  <source type="image/webp" [attr.srcset]="image.webpSet" [attr.sizes]="sizes" />
                  @if (image.jpegSet) {
                    <source type="image/jpeg" [attr.srcset]="image.jpegSet" [attr.sizes]="sizes" />
                  }
                  <img
                    [src]="image.fallback"
                    [attr.width]="image.width"
                    [attr.height]="image.height"
                    [alt]="image.alt"
                    [attr.loading]="$first ? 'eager' : 'lazy'"
                    [attr.fetchpriority]="$first ? 'high' : null"
                    decoding="async"
                  />
                </picture>
              </figure>
            </li>
          }
        </ul>
        @if (images().length > 1) {
          <p class="gallery__count">{{ images().length }} images</p>
        }
      } @else {
        <p class="gallery__none">Photographs of this piece are on their way.</p>
      }
    </section>
  `,
  styles: `
    .gallery__list { display: flex; gap: var(--grid-gap-mobile); margin: 0 calc(var(--margin-mobile) * -1); padding: 0; list-style: none; overflow-x: auto; scroll-snap-type: x mandatory; scrollbar-width: none; }
    .gallery__list::-webkit-scrollbar { display: none; }
    .gallery__list:focus-visible { outline: 2px solid var(--ink); outline-offset: -2px; }
    .gallery__item { flex: 0 0 100%; scroll-snap-align: start; }
    figure { margin: 0; }
    img { display: block; width: 100%; height: auto; aspect-ratio: var(--image-ratio); object-fit: cover; background: var(--surface-sunken, var(--line)); }
    .gallery__count, .gallery__none { margin: var(--space-2) 0 0; font: 400 13px/20px var(--font-sans); color: var(--ink-muted); }
    @media (min-width: 64rem) {
      .gallery__list { display: grid; grid-template-columns: 1fr 1fr; margin: 0; overflow: visible; }
      .gallery__count { display: none; }
    }
  `,
})
export class ProductGalleryComponent {
  readonly images = input.required<readonly ProductImage[]>();

  protected readonly sizes = SIZES;
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
}
