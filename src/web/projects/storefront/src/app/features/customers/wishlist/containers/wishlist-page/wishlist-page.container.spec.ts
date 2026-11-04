import { RESPONSE_INIT } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Meta } from '@angular/platform-browser';
import { provideRouter } from '@angular/router';
import { Store, provideStore } from '@ngrx/store';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { CustomerAuthActions, customerAuthFeature } from '../../../../../core/customer-auth';
import { polyfillDialog } from '../../../../../layout/dialog/dialog.testing';
import { provideSiteSettings } from '../../../../storefront/site-settings';
import { ChangeResult, ListResult, WishlistItem } from '../../models/wishlist.model';
import { GUEST_KEY } from '../../services/guest-wishlist.storage';
import { WishlistApi } from '../../services/wishlist-api.service';
import { provideWishlist } from '../../wishlist.providers';
import { WishlistPageContainer } from './wishlist-page.container';

const item = (id: number, extra: Partial<WishlistItem> = {}): WishlistItem => ({
  productId: id, name: `Product ${id}`, path: `/girls/dresses/product-${id}`, price: 3490, wasPrice: null, savedPrice: 3490, isAvailable: true,
  imageUrl: `/media/${id}-640.webp`, imageFallbackUrl: `/media/${id}-640.jpg`, imageWidth: 640, notes: [], savedAt: '2026-11-04T09:00:00Z', ...extra,
});
const ok = (items: WishlistItem[]): ListResult => ({ status: 'ok', page: { items, page: 1, pageSize: 24, totalCount: items.length, totalPages: 1 } });

function render(list: ListResult, signedIn = true, responseInit?: ResponseInit) {
  polyfillDialog();
  const remove = vi.fn((_id: number): ChangeResult => ({ status: 'ok' }));
  TestBed.configureTestingModule({
    providers: [
      provideRouter([]),
      provideStore({ [customerAuthFeature.name]: customerAuthFeature.reducer }),
      provideSiteSettings(),
      provideWishlist(),
      {
        provide: WishlistApi,
        useValue: {
          list: () => of(list),
          ids: () => of({ status: 'ok', ids: [1, 2, 3] }),
          add: () => of({ status: 'ok' }),
          remove: (id: number) => of(remove(id)),
          merge: () => of({ status: 'ok' }),
        },
      },
      ...(responseInit ? [{ provide: RESPONSE_INIT, useValue: responseInit }] : []),
    ],
  });
  const store = TestBed.inject(Store);
  store.dispatch(
    CustomerAuthActions.sessionLoaded({ result: signedIn ? { status: 'ok', session: { firstName: 'Dev', emailVerified: true, csrfToken: 'x' } } : { status: 'signedOut' } }),
  );
  const fixture = TestBed.createComponent(WishlistPageContainer);
  fixture.detectChanges();
  TestBed.flushEffects?.();
  fixture.detectChanges();
  return { fixture, el: fixture.nativeElement as HTMLElement, remove };
}

describe('WishlistPageContainer', () => {
  beforeEach(() => localStorage.clear());

  it('WSL-9 has one h1, a breadcrumb, and lists each product as an article under its own heading with a plain link', () => {
    const { el } = render(ok([item(1), item(2)]));

    expect(el.querySelectorAll('h1')).toHaveLength(1);
    expect(el.querySelector('nav[aria-label="Breadcrumb"]')).not.toBeNull();
    const cards = el.querySelectorAll('ul.rp-grid > li > article.rp-card');
    expect(cards).toHaveLength(2);
    expect(cards[0].querySelector('h2 a')!.getAttribute('href')).toBe('/girls/dresses/product-1');
    expect(el.querySelector('.rp-count-note')!.textContent).toContain('2 saved');
  });

  it('WSL-9 each product has a heart button that is pressed and named for the product and its state', () => {
    const { el } = render(ok([item(1)]));

    const heart = el.querySelector('button[aria-pressed]')!;
    expect(heart.getAttribute('aria-pressed')).toBe('true');
    expect(heart.getAttribute('aria-label')).toBe('Remove Product 1 from wishlist');
  });

  it('WSL-6 shows the sale price with the struck old price and the saving, and a price drop', () => {
    const { el } = render(ok([item(1, { price: 3490, wasPrice: 4990, notes: ['OnSale', 'PriceDropped'], savedPrice: 4500 })]));
    const text = el.querySelector('article')!.textContent ?? '';

    expect(text).toContain('Rs. 3,490');
    expect(el.querySelector('article s')!.textContent).toContain('4,990');
    expect(text).toContain('Save Rs. 1,500');
    expect(text).toContain('−30%');
    expect(text).toContain('Price dropped from Rs. 4,500');
  });

  it('WSL-6 a sold-out product says so and offers no Choose size button', () => {
    const { el } = render(ok([item(1, { isAvailable: false, notes: ['SoldOut'] })]));
    const card = el.querySelector('article')!;

    expect(card.textContent).toContain('Sold out');
    expect(card.textContent).not.toContain('Choose size');
  });

  it('WSL-5 a product no longer published is shown as unavailable and can be removed', () => {
    const { el, remove } = render(ok([item(9, { name: null, path: null, price: null, imageUrl: null, imageFallbackUrl: null, notes: ['Unavailable'], isAvailable: false })]));
    const card = el.querySelector('article')!;

    expect(card.textContent).toContain('No longer available');
    expect(card.querySelector('a')).toBeNull();
    const button = Array.from(card.querySelectorAll('button')).find((b) => b.textContent?.includes('Remove from wishlist'))!;
    button.click();
    expect(remove).toHaveBeenCalledWith(9);
  });

  it('WSL-2 a signed-out shopper is told what is remembered on this device and asked to sign in', () => {
    localStorage.setItem(GUEST_KEY, JSON.stringify([1, 2]));
    const { el } = render(ok([]), false);

    const notice = el.querySelector('.rp-notice')!;
    expect(notice.textContent).toContain('2 saved on this device');
    expect(notice.querySelector('a')!.getAttribute('href')).toContain('/account/sign-in');
    expect(el.querySelector('ul.rp-grid')).toBeNull();
  });

  it('shows an empty list as a sentence with one link onward', () => {
    const { el } = render(ok([]));

    expect(el.textContent).toContain('Nothing saved yet');
    expect(el.querySelectorAll('main a, .rp-wrap a').length).toBeGreaterThan(0);
  });

  it('WSL-9 is noindex and its server response is never cached', () => {
    const responseInit: ResponseInit = { headers: {} };
    render(ok([item(1)]), true, responseInit);

    expect(TestBed.inject(Meta).getTag('name="robots"')?.content).toContain('noindex');
    expect(new Headers(responseInit.headers).get('Cache-Control')).toBe('private, no-store');
  });
});
