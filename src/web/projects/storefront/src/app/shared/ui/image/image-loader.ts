import { IMAGE_LOADER, ImageLoaderConfig } from '@angular/common';
import { Provider } from '@angular/core';

/** Widths every storefront image is offered at. Derivative files are produced for these (ADR 0018). */
export const RP_IMAGE_WIDTHS = [400, 640, 828, 1080, 1440, 1920] as const;

export const RP_IMAGE_SRCSET = RP_IMAGE_WIDTHS.map((w) => `${w}w`).join(', ');

/** `sizes` presets per layout (design system, Implementation.md). */
export const RP_IMAGE_SIZES = {
  card: '(min-width: 64rem) 25vw, (min-width: 40rem) 33vw, 50vw',
  gallery: '(min-width: 64rem) 29vw, 100vw',
  hero: '100vw',
} as const;

/**
 * The one place that decides where image derivatives live. Today it appends `?w=<width>`; when the
 * published-catalog derivatives (hashed WebP/JPEG names) exist, only this function changes.
 */
export function rpImageLoader(config: ImageLoaderConfig): string {
  if (!config.width) {
    return config.src;
  }
  return `${config.src}?w=${config.width}`;
}

export function provideRpImageLoader(): Provider {
  return { provide: IMAGE_LOADER, useValue: rpImageLoader };
}
