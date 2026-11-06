import { ApplicationRef } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideEffects } from '@ngrx/effects';
import { of } from 'rxjs';
import { afterEach } from 'vitest';
import { provideCart } from '../../../../cart/carts/cart.providers';
import { CartApi } from '../../../../cart/carts/services/cart-api.service';
import { provideState, provideStore } from '@ngrx/store';
import { wishlistFeature } from '../../../../../features/customers/wishlist/store/wishlist.reducer';
import { provideRouter } from '@angular/router';
import { ListingTile } from '../../models/listing.model';
import { ProductTileComponent } from './product-tile.component';

const tile = (extra: Partial<ListingTile> = {}): ListingTile => ({
  productId: 1,
  path: '/girls/dresses/floral',
  name: 'Floral dress',
  price: 800,
  compareAtPrice: null,
  savingPercent: 0,
  savingAmount: 0,
  colourCount: 1,
  hasSoldOutSizes: false,
  sizes: [],
  image: null,
  ...extra,
});

function render(t: ListingTile): HTMLElement {
  TestBed.configureTestingModule({ providers: [provideRouter([]), provideStore({}), provideState(wishlistFeature)] });
  const fixture = TestBed.createComponent(ProductTileComponent);
  fixture.componentRef.setInput('tile', t);
  fixture.detectChanges();
  return fixture.nativeElement as HTMLElement;
}

describe('ProductTileComponent', () => {
  it('PR-1 a discounted tile shows the minus badge, the sale price, the struck old price and the saving in words', () => {
    const el = render(tile({ price: 800, compareAtPrice: 1000, savingPercent: 20, savingAmount: 200 }));

    expect(el.querySelector('.rp-tag.rp-tag--sale')?.textContent?.trim()).toBe('−20%');
    expect(el.querySelector('.rp-price.rp-price--sale')?.textContent).toContain('Rs. 800');
    expect(el.querySelector('.rp-price s')?.textContent).toContain('Rs. 1,000');
    expect(el.querySelector('.rp-price .sr-only')?.textContent).toContain('Now');
    expect(el.querySelector('.rp-price s .sr-only')?.textContent).toContain('Was');
    expect(el.querySelector('.rp-save')?.textContent?.trim()).toBe('Save Rs. 200');
  });

  it('PR-2 the saving shown is the server amount, not a subtraction', () => {
    const el = render(tile({ price: 800, compareAtPrice: 1000, savingPercent: 20, savingAmount: 250 }));

    expect(el.querySelector('.rp-save')?.textContent?.trim()).toBe('Save Rs. 250');
  });

  it('PR-3 a tile without a discount has no badge, strike or saving line', () => {
    const el = render(tile());

    expect(el.querySelector('.rp-tag')).toBeNull();
    expect(el.querySelector('.rp-price s')).toBeNull();
    expect(el.querySelector('.rp-save')).toBeNull();
    expect(el.querySelector('.rp-price')?.textContent).toContain('Rs. 800');
  });

  it('PR-8 the size line lists every size and strikes the sold-out ones with "sold out" for screen readers', () => {
    const el = render(
      tile({
        hasSoldOutSizes: true,
        sizes: [
          { code: '3-4y', name: '3-4Y', soldOut: false },
          { code: '5-6y', name: '5-6Y', soldOut: true },
          { code: '7-8y', name: '7-8Y', soldOut: false },
        ],
      }),
    );

    const items = [...el.querySelectorAll('.rp-avail li')];
    expect(items.map((i) => i.textContent?.replace(/\s+/g, ' ').trim())).toEqual(['3-4Y', '5-6Y sold out', '7-8Y']);
    expect(items[1].querySelector('s')).not.toBeNull();
    expect(items[1].querySelector('.sr-only')?.textContent).toContain('sold out');
    expect(items[0].querySelector('s')).toBeNull();
  });

  it('PR-8 a tile with no sizes renders no size line', () => {
    expect(render(tile()).querySelector('.rp-avail')).toBeNull();
  });

  it('PR-9 CRD-13 the plain card has no add-to-bag control of its own: one heart and one link (the bar is added in the browser)', () => {
    const el = render(tile({ sizes: [{ code: '3-4y', name: '3-4Y', soldOut: false }] }));

    expect(el.querySelector('button:not([rpWishlistHeart])')).toBeNull();
    expect(el.querySelectorAll('button[aria-pressed]')).toHaveLength(1);
    expect(el.querySelectorAll('a')).toHaveLength(1);
  });
});

const picture = (name: string) => ({ altText: name, width: 800, height: 1000, sources: [{ width: 640, webp: '/media/' + name + '-640.webp', jpeg: '/media/' + name + '-640.jpg' }] });
const quickSizes = [
  { code: '2y', name: '2Y', soldOut: false, variantId: 1, availability: 'InStock', colourName: 'Rose' },
  { code: '3y', name: '3Y', soldOut: false, variantId: 2, availability: 'LowStock', colourName: 'Rose' },
];

function renderEnhanced(t: ListingTile, touch = false): { el: HTMLElement; host: HTMLElement } {
  vi.stubGlobal('matchMedia', () => ({ matches: touch, addEventListener: () => undefined, removeEventListener: () => undefined }));
  const api = { load: () => of({ status: 'ok' }), add: () => of({ status: 'ok' }), setQuantity: () => of({ status: 'ok' }), remove: () => of({ status: 'ok' }), applyCode: () => of({ status: 'ok' }), removeCode: () => of({ status: 'ok' }) };
  TestBed.configureTestingModule({ providers: [provideRouter([]), provideStore(), provideEffects(), provideState(wishlistFeature), provideCart(), { provide: CartApi, useValue: api }] });
  const fixture = TestBed.createComponent(ProductTileComponent);
  fixture.componentRef.setInput('tile', t);
  fixture.detectChanges();
  TestBed.inject(ApplicationRef).tick();
  fixture.detectChanges();
  return { el: fixture.nativeElement as HTMLElement, host: fixture.nativeElement as HTMLElement };
}

afterEach(() => vi.unstubAllGlobals());

describe('ProductTileComponent card extras', () => {
  it('CRD-3 shows at most one tag: sale, else Low stock, else New, else none', () => {
    const tag = (extra: Partial<ListingTile>) => {
      TestBed.resetTestingModule();
      return [...render(tile(extra)).querySelectorAll('.rp-tag')].map((e) => e.textContent!.trim());
    };
    expect(tag({ savingPercent: 25, hasLowStock: true, isNew: true })).toEqual(['−25%']);
    expect(tag({ hasLowStock: true, isNew: true })).toEqual(['Low stock']);
    expect(tag({ isNew: true })).toEqual(['New']);
    expect(tag({})).toEqual([]);
  });

  it('CRD-1 the second picture is not requested at page load, but after the first pointer enter, lazily', () => {
    const { el, host } = renderEnhanced(tile({ image: picture('a'), secondImage: picture('b') }));
    expect(el.querySelectorAll('img.rp-tile-img').length).toBe(1);
    host.dispatchEvent(new Event('pointerenter'));
    TestBed.inject(ApplicationRef).tick();
    const imgs = el.querySelectorAll<HTMLImageElement>('img.rp-tile-img');
    expect(imgs.length).toBe(2);
    expect(imgs[1].getAttribute('loading')).toBe('lazy');
    expect(imgs[1].alt).toBe('b');
    expect(el.querySelector('.rp-slide--alt')).not.toBeNull();
  });

  it('CRD-1 focus also asks for the second picture, and a product with one picture never gets one', () => {
    const withTwo = renderEnhanced(tile({ image: picture('a'), secondImage: picture('b') }));
    withTwo.host.dispatchEvent(new Event('focusin'));
    TestBed.inject(ApplicationRef).tick();
    expect(withTwo.el.querySelectorAll('img.rp-tile-img').length).toBe(2);
    TestBed.resetTestingModule();
    const withOne = renderEnhanced(tile({ image: picture('a'), secondImage: null }));
    withOne.host.dispatchEvent(new Event('pointerenter'));
    TestBed.inject(ApplicationRef).tick();
    expect(withOne.el.querySelectorAll('img.rp-tile-img').length).toBe(1);
    expect(withOne.el.querySelector('.rp-pagedots')).toBeNull();
  });

  it('CRD-2 on touch the two pictures are a strip with two dots, the first active', () => {
    const { el } = renderEnhanced(tile({ image: picture('a'), secondImage: picture('b') }), true);
    expect(el.querySelectorAll('.rp-swipe .rp-slide').length).toBe(2);
    const dots = [...el.querySelectorAll('.rp-pagedots i')];
    expect(dots.length).toBe(2);
    expect(dots[0].classList.contains('is-on')).toBe(true);
    expect(el.querySelector('.rp-pagedots')!.getAttribute('aria-hidden')).toBe('true');
  });

  it('CRD-6 a + button opens the bar on touch and says whether it is open', () => {
    const { el } = renderEnhanced(tile({ image: picture('a'), sizes: quickSizes }), true);
    const plus = el.querySelector<HTMLButtonElement>('button.rp-qa__plus')!;
    expect(plus.getAttribute('aria-label')).toBe('Quick add to bag');
    expect(plus.getAttribute('aria-expanded')).toBe('false');
    plus.click();
    TestBed.inject(ApplicationRef).tick();
    expect(plus.getAttribute('aria-expanded')).toBe('true');
    expect(el.querySelector('.rp-qa__host')!.classList.contains('is-open')).toBe(true);
    expect(el.querySelector('rp-quick-add-bar')).not.toBeNull();
  });

  it('CRD-11 Escape puts the bar away and returns focus to the card link; coming back to the bar shows it again', () => {
    const { el, host } = renderEnhanced(tile({ image: picture('a'), sizes: quickSizes }));
    const card = el.querySelector('article.rp-card')!;
    el.querySelector<HTMLElement>('.rp-qa__sizes button')!.focus();
    host.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
    TestBed.inject(ApplicationRef).tick();
    expect(card.classList.contains('is-dismissed')).toBe(true);
    expect(document.activeElement).toBe(el.querySelector('h3 a'));
    el.querySelector<HTMLElement>('.rp-qa__host')!.dispatchEvent(new Event('focusin', { bubbles: true }));
    TestBed.inject(ApplicationRef).tick();
    expect(card.classList.contains('is-dismissed')).toBe(false);
  });

  it('CRD-6 a card whose sizes carry no variant (an older response) has no + and no bar', () => {
    const { el } = renderEnhanced(tile({ image: picture('a'), sizes: [{ code: '2y', name: '2Y', soldOut: false }] }));
    expect(el.querySelector('.rp-qa__plus')).toBeNull();
    expect(el.querySelector('rp-quick-add-bar')).toBeNull();
  });

  it('CRD-13 CRD-15 the plain card parts stay: the first picture, name link, price and size line', () => {
    const { el } = renderEnhanced(tile({ image: picture('a'), sizes: quickSizes }));
    expect(el.querySelector('picture img')!.getAttribute('alt')).toBe('a');
    expect(el.querySelector('h3 a')!.getAttribute('href')).toBe('/girls/dresses/floral');
    expect(el.querySelector('.rp-price')!.textContent).toContain('800');
    expect(el.querySelectorAll('.rp-avail li').length).toBe(2);
  });
});
