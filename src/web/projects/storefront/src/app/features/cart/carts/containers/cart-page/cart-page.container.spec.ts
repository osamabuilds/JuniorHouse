import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { RESPONSE_INIT } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideEffects } from '@ngrx/effects';
import { Store, provideStore } from '@ngrx/store';
import { provideSiteSettings } from '../../../../storefront/site-settings';
import { Cart, CartLine } from '../../models/cart.model';
import { provideCart } from '../../cart.providers';
import { CartActions } from '../../store/cart.actions';
import { CartPageContainer } from './cart-page.container';

const line = (variantId: number, quantity: number, over: Partial<CartLine> = {}): CartLine => ({
  variantId, productId: 1, name: `Dress ${variantId}`, path: '/girls/dresses/dress', colour: 'Red', size: '2-3Y', sku: 'S', unitPrice: 1000, compareAtPrice: null,
  quantity, lineTotal: 1000 * quantity, available: 9, previousPrice: null, issues: [], imageUrl: null, imageFallbackUrl: null, imageAlt: null, ...over,
});

const cartOf = (lines: CartLine[], hasProblems = false): Cart => ({ lines, itemCount: lines.reduce((n, l) => n + l.quantity, 0), subtotal: lines.reduce((n, l) => n + l.lineTotal, 0), hasProblems });

function render(responseInit?: ResponseInit) {
  TestBed.configureTestingModule({
    providers: [
      provideRouter([]), provideHttpClient(), provideHttpClientTesting(), provideStore(), provideEffects(), provideSiteSettings(), provideCart(),
      ...(responseInit ? [{ provide: RESPONSE_INIT, useValue: responseInit }] : []),
    ],
  });
  const store = TestBed.inject(Store);
  const fixture = TestBed.createComponent(CartPageContainer);
  fixture.detectChanges();
  const show = (cart: Cart) => {
    store.dispatch(CartActions.loadDone({ result: { status: 'ok', cart } }));
    fixture.detectChanges();
  };
  return { fixture, el: fixture.nativeElement as HTMLElement, store, show };
}


describe('CartPageContainer', () => {
  it('SEO-05 the response is never cached', () => {
    const init: ResponseInit = {};
    render(init);

    expect(new Headers(init.headers).get('Cache-Control')).toBe('private, no-store');
  });

  it('CRT-2 an empty basket says so, with one clear next step and a sign-in line', () => {
    const { el, show } = render();
    show(cartOf([]));

    expect(el.querySelectorAll('h1').length).toBe(1);
    expect(el.querySelector('h1')!.textContent).toContain('Your bag is empty');
    expect(el.querySelector('a.rp-btn--action')!.textContent).toContain('Start shopping');
    expect(el.textContent).toContain('Sign in');
  });

  it('CRT-1 a basket shows one h1, a count, every line and the server\'s subtotal', () => {
    const { el, show } = render();
    show(cartOf([line(1, 2), line(2, 1)]));

    expect(el.querySelectorAll('h1').length).toBe(1);
    expect(el.querySelector('.rp-count-note')!.textContent).toContain('3 items');
    expect(el.querySelectorAll('.rp-bag-lines > li').length).toBe(2);
    expect(el.querySelector('rp-cart-summary')!.textContent).toContain('Rs. 3,000');
    expect(el.querySelector('rp-cart-summary dl')!.getAttribute('aria-live')).toBe('polite');
  });

  it('a quantity press and Remove go to the store', () => {
    const { el, store, show } = render();
    show(cartOf([line(1, 2)]));
    const dispatch = vi.spyOn(store, 'dispatch');

    (el.querySelector('button[aria-label="Increase quantity"]') as HTMLButtonElement).click();
    [...el.querySelectorAll('button')].find((b) => b.getAttribute('aria-label')?.startsWith('Remove'))!.click();

    expect(dispatch).toHaveBeenCalledWith(CartActions.setQuantity({ variantId: 1, quantity: 3 }));
    expect(dispatch).toHaveBeenCalledWith(CartActions.remove({ variantId: 1 }));
  });

  it('CRT-4 a refusal is announced as an alert above the lines', () => {
    const { fixture, el, store, show } = render();
    show(cartOf([line(1, 2)]));

    store.dispatch(CartActions.changeDone({ kind: 'set', result: { status: 'refused', message: 'Only 3 are in stock.' } }));
    fixture.detectChanges();

    expect(el.querySelector('[role="alert"]')!.textContent).toContain('Only 3 are in stock.');
  });

  it('Checkout waits for the next release and says why, in words', () => {
    const { el, show } = render();
    show(cartOf([line(1, 1)]));
    const button = [...el.querySelectorAll('button')].find((b) => b.textContent?.trim() === 'Checkout') as HTMLButtonElement;

    expect(button.disabled).toBe(true);
    expect(button.getAttribute('aria-describedby')).toBe('bag-checkout-note');
    expect(el.querySelector('#bag-checkout-note')!.textContent).toContain('next release');
  });

  it('CRT-7 a basket with a problem line tells the shopper to fix it', () => {
    const { el, show } = render();
    show(cartOf([line(1, 5, { issues: ['low-stock'], available: 1 })], true));

    expect(el.querySelector('#bag-fix')!.textContent).toContain('Fix the items');
    expect(el.querySelector('.rp-bag-note')!.textContent).toContain('Only 1 in stock');
  });
});
