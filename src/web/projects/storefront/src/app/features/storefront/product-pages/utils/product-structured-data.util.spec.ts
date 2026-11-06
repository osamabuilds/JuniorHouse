import { ProductPage, SizeOption } from '../models/product-page.model';
import { offerAvailability, productStructuredData } from './product-structured-data.util';

const size = (availability: SizeOption['availability'], code = 'a'): SizeOption => ({ code, name: code, availability, selectable: true, variantId: 1 });

const page = (over: Partial<ProductPage> = {}): ProductPage =>
  ({
    productId: 1,
    path: '/girls/dresses/floral',
    name: 'Floral dress',
    description: 'Soft cotton.',
    sku: 'FD-1',
    price: { price: 1200, compareAtPrice: null, savingPercent: 0, savingAmount: 0, isFrom: false },
    sizes: [size('InStock')],
    images: [{ altText: 'x', width: 800, height: 1000, sources: [{ width: 320, webp: '/m/a-320.webp', jpeg: '/m/a-320.jpg' }, { width: 640, webp: '/m/a-640.webp', jpeg: '/m/a-640.jpg' }] }],
    ...over,
  }) as ProductPage;

describe('product structured data', () => {
  it('SLD-6 a product with a purchasable size says InStock; low stock still counts', () => {
    expect(offerAvailability(page())).toBe('https://schema.org/InStock');
    expect(offerAvailability(page({ sizes: [size('SoldOut', 'a'), size('LowStock', 'b')] }))).toBe('https://schema.org/InStock');
  });

  it('SLD-6 a product whose every size is sold out or not yet available says OutOfStock', () => {
    expect(offerAvailability(page({ sizes: [size('SoldOut', 'a'), size('NotYetAvailable', 'b')] }))).toBe('https://schema.org/OutOfStock');
    expect(offerAvailability(page({ sizes: [] }))).toBe('https://schema.org/OutOfStock');
  });

  it('SEO-03 builds Product + Offer with absolute urls, the PKR price from the server and no stock count', () => {
    const ld = productStructuredData(page(), 'https://romp.example/') as Record<string, any>;
    expect(ld['@type']).toBe('Product');
    expect(ld['image']).toBe('https://romp.example/m/a-640.jpg');
    expect(ld['offers']).toEqual({ '@type': 'Offer', url: 'https://romp.example/girls/dresses/floral', priceCurrency: 'PKR', price: '1200.00', availability: 'https://schema.org/InStock' });
    expect(JSON.stringify(ld)).not.toMatch(/inventoryLevel|quantity/i);
  });

  it('SLD-6 a sold-out product page still has an Offer, marked OutOfStock', () => {
    const ld = productStructuredData(page({ sizes: [size('SoldOut')] }), 'https://romp.example') as Record<string, any>;
    expect(ld['offers'].availability).toBe('https://schema.org/OutOfStock');
  });

  it('SEO-03 a product without a price has no Offer', () => {
    expect((productStructuredData(page({ price: null }), 'https://romp.example') as Record<string, unknown>)['offers']).toBeUndefined();
  });
});
