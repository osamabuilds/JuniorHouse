import { ProductPage } from '../models/product-page.model';

const IN_STOCK = 'https://schema.org/InStock';
const OUT_OF_STOCK = 'https://schema.org/OutOfStock';

/** The Offer availability word (SLD-6): InStock when any size can be bought, otherwise OutOfStock. A hint for search engines, no count. */
export function offerAvailability(page: Pick<ProductPage, 'sizes'>): string {
  return page.sizes.some((s) => s.availability === 'InStock' || s.availability === 'LowStock') ? IN_STOCK : OUT_OF_STOCK;
}

/**
 * The Product + Offer JSON-LD of a product page (SEO-03). Price, currency and availability are the server's words; nothing is computed here.
 * `site` is the public origin, so every URL in it is absolute. A product with no price has no Offer.
 */
export function productStructuredData(page: ProductPage, site: string): object {
  const origin = site.replace(/\/$/, '');
  const absolute = (url: string) => (/^https?:\/\//.test(url) ? url : `${origin}${url.startsWith('/') ? '' : '/'}${url}`);
  const first = page.images[0];
  const source = first ? (first.sources.find((s) => s.width >= 640) ?? first.sources[first.sources.length - 1]) : undefined;
  const image = source ? absolute(source.jpeg ?? source.webp) : null;
  return {
    '@context': 'https://schema.org',
    '@type': 'Product',
    name: page.name,
    ...(page.description.trim() ? { description: page.description.trim() } : {}),
    ...(page.sku ? { sku: page.sku } : {}),
    ...(image ? { image } : {}),
    ...(page.price
      ? {
          offers: {
            '@type': 'Offer',
            url: absolute(page.path),
            priceCurrency: 'PKR',
            price: page.price.price.toFixed(2),
            availability: offerAvailability(page),
          },
        }
      : {}),
  };
}
