import { NgOptimizedImage } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  input,
  isDevMode,
  signal,
} from '@angular/core';
import { ImageAsset } from '../../models/image-asset.model';
import { RP_IMAGE_SIZES, RP_IMAGE_SRCSET } from './image-loader';

const SAFE_BLUR = /^data:image\/(png|jpeg|webp);base64,[A-Za-z0-9+/=]+$/;

/**
 * Every photograph on the storefront. A fixed 4:5 box, so nothing shifts while the file loads; the
 * `rp-ph` tonal placeholder fills it when there is no file or the file fails.
 *
 * Only one image per page gets `priority` (the LCP image). A second view is requested on the first
 * pointer-enter or focus, never on load, and crossfades in on hover-capable devices.
 */
@Component({
  selector: 'rp-image',
  imports: [NgOptimizedImage],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './image.component.scss',
  host: {
    '(pointerenter)': 'warm($event)',
    '(focusin)': 'warm()',
  },
  template: `
    <div class="rp-media">
      @if (primary(); as img) {
        <img
          [ngSrc]="img.src"
          [ngSrcset]="srcset"
          [sizes]="sizes()"
          [width]="img.width"
          [height]="img.height"
          [alt]="alt()"
          [priority]="priority()"
          [placeholder]="blur()"
          (error)="failed.set(true)"
        />
      } @else {
        <div class="rp-ph rp-ph--{{ tone() }}" aria-hidden="true"></div>
      }
      @if (secondView(); as img) {
        <img
          class="rp-image__second"
          [ngSrc]="img.src"
          [ngSrcset]="srcset"
          [sizes]="sizes()"
          [width]="img.width"
          [height]="img.height"
          alt=""
          aria-hidden="true"
        />
      }
    </div>
  `,
})
export class ImageComponent {
  readonly image = input<ImageAsset | null | undefined>(null);
  /** Optional second view (back, fabric close-up). */
  readonly second = input<ImageAsset | null | undefined>(null);
  readonly sizes = input<string>(RP_IMAGE_SIZES.card);
  /** The page's single LCP image: eager, `fetchpriority="high"`, preloaded on the server. */
  readonly priority = input(false);
  /** Decorative images get an empty alt; every other image must have alt text. */
  readonly decorative = input(false);
  /** Placeholder tone while there is no file (design tokens `tone-*`). */
  readonly tone = input<'oat' | 'stone' | 'clay' | 'sage' | 'sand' | 'dusk'>('oat');

  protected readonly srcset = RP_IMAGE_SRCSET;
  protected readonly failed = signal(false);
  private readonly warmed = signal(false);

  protected readonly primary = computed(() => (this.failed() ? null : (this.image() ?? null)));
  protected readonly secondView = computed(() => (this.warmed() ? (this.second() ?? null) : null));

  protected readonly blur = computed(() => {
    const url = this.image()?.blurDataUrl;
    return url && SAFE_BLUR.test(url) ? url : false;
  });

  protected readonly alt = computed(() => {
    if (this.decorative()) {
      return '';
    }
    const alt = this.image()?.alt?.trim() ?? '';
    if (!alt && this.image() && isDevMode()) {
      throw new Error('rp-image needs alt text, or decorative="true" for a decorative image.');
    }
    return alt;
  });

  /** Requests the second view. Called on first focus or mouse pointer-enter, and by a parent card. */
  warm(event?: PointerEvent): void {
    if (event && event.pointerType !== 'mouse') {
      return;
    }
    this.warmed.set(true);
  }
}
