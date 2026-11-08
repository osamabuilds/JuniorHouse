import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideEffects } from '@ngrx/effects';
import { Store, provideStore } from '@ngrx/store';
import { provideRouter } from '@angular/router';
import { Observable, of } from 'rxjs';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { provideCart } from '../../../../cart/carts/cart.providers';
import { Cart, CartResult } from '../../../../cart/carts/models/cart.model';
import { CartApi } from '../../../../cart/carts/services/cart-api.service';
import { CartActions } from '../../../../cart/carts/store/cart.actions';
import { selectCartDrawerOpen, selectCartItemCount } from '../../../../cart/carts/store/cart.selectors';
import { ListingTile } from '../../models/listing.model';
import { QuickAddContainer } from './quick-add.container';

const emptyCart = { lines: [], itemCount: 0, subtotal: 0 } as unknown as Cart;
const cartWithOne = { lines: [{ variantId: 2 }], itemCount: 1, subtotal: 800 } as unknown as Cart;

class FakeApi {
  add: (v: number, q: number) => Observable<CartResult> = () => of({ status: 'ok', cart: cartWithOne });
  load = () => of<CartResult>({ status: 'ok', cart: emptyCart });
  setQuantity = () => of<CartResult>({ status: 'ok', cart: emptyCart });
  remove = () => of<CartResult>({ status: 'ok', cart: emptyCart });
  applyCode = () => of<CartResult>({ status: 'ok', cart: emptyCart });
  removeCode = () => of<CartResult>({ status: 'ok', cart: emptyCart });
}

const tile = (over: Partial<ListingTile> = {}): ListingTile => ({
  productId: 1, path: '/girls/dresses/floral', name: 'Floral', price: 800, compareAtPrice: null, savingPercent: 0, savingAmount: 0, colourCount: 1, hasSoldOutSizes: false,
  sizes: [
    { code: '2y', name: '2Y', soldOut: false, variantId: 1, availability: 'InStock', colourName: 'Rose' },
    { code: '3y', name: '3Y', soldOut: false, variantId: 2, availability: 'LowStock', colourName: 'Rose' },
  ],
  image: null,
  ...over,
});

function render(api = new FakeApi(), t = tile()): { f: ComponentFixture<QuickAddContainer>; el: HTMLElement; store: Store; stale: ReturnType<typeof vi.fn> } {
  TestBed.configureTestingModule({ providers: [provideRouter([]), provideStore(), provideEffects(), provideCart(), { provide: CartApi, useValue: api }] });
  const f = TestBed.createComponent(QuickAddContainer);
  f.componentRef.setInput('tile', t);
  f.detectChanges();
  const stale = vi.fn();
  f.componentInstance.stale.subscribe(stale);
  return { f, el: f.nativeElement as HTMLElement, store: TestBed.inject(Store), stale };
}
const chip = (el: HTMLElement, name: string) => [...el.querySelectorAll<HTMLButtonElement>('.rp-qa__sizes button')].find((b) => b.textContent!.trim().startsWith(name))!;

afterEach(() => vi.useRealTimers());

describe('QuickAddContainer', () => {
  it('CRD-5 CRD-10 pressing a size sends that variant once, quietly, with no price', () => {
    const { el, store } = render();
    const dispatch = vi.spyOn(store, 'dispatch');
    chip(el, '3Y').click();
    const adds = dispatch.mock.calls.filter(([a]) => (a as unknown as { type: string }).type === CartActions.add.type);
    expect(adds).toEqual([[CartActions.add({ variantId: 2, quantity: 1, quiet: true })]]);
  });

  it('CRD-5 after the add it says what was added, the bag count changes and the drawer stays closed', () => {
    const { f, el, store } = render();
    chip(el, '3Y').click();
    f.detectChanges();
    expect(el.querySelector('[role="status"]')!.textContent).toContain('Added, size 3Y');
    expect(el.querySelector('[role="status"]')!.textContent).not.toContain('Rose');
    expect(store.selectSignal(selectCartItemCount)()).toBe(1);
    expect(store.selectSignal(selectCartDrawerOpen)()).toBe(false);
  });

  it('REC-7 REC-12 a successful add says so to the page once (so a recommendation row can count it), a refused one does not', () => {
    const ok = render();
    const bagged = vi.fn();
    ok.f.componentInstance.bagged.subscribe(bagged);
    chip(ok.el, '3Y').click();
    ok.f.detectChanges();
    expect(bagged).toHaveBeenCalledTimes(1);

    TestBed.resetTestingModule();
    const api = new FakeApi();
    api.add = () => of({ status: 'refused', message: 'That size has just sold out.' });
    const refused = render(api);
    const baggedRefused = vi.fn();
    refused.f.componentInstance.bagged.subscribe(baggedRefused);
    chip(refused.el, '3Y').click();
    refused.f.detectChanges();
    expect(baggedRefused).not.toHaveBeenCalled();
  });

  it('CRD-5 a product with several colours names the colour that was added', () => {
    const { f, el } = render(new FakeApi(), tile({ colourCount: 2 }));
    chip(el, '3Y').click();
    f.detectChanges();
    expect(el.querySelector('[role="status"]')!.textContent).toContain('Added, size 3Y, Rose');
  });

  it('CRD-5 View bag opens the bag drawer', () => {
    const { f, el, store } = render();
    chip(el, '2Y').click();
    f.detectChanges();
    [...el.querySelectorAll('button')].find((b) => b.textContent!.trim() === 'View bag')!.click();
    expect(store.selectSignal(selectCartDrawerOpen)()).toBe(true);
  });

  it('CRD-9 a refused add shows the server words, keeps the sizes to try another, and asks the page to refresh the card', () => {
    const api = new FakeApi();
    api.add = () => of({ status: 'refused', message: 'That size has just sold out.' });
    const { f, el, store, stale } = render(api);
    chip(el, '3Y').click();
    f.detectChanges();
    expect(el.querySelector('[role="alert"]')!.textContent).toContain('just sold out');
    expect(el.querySelectorAll('.rp-qa__sizes button').length).toBe(2);
    expect(store.selectSignal(selectCartItemCount)()).toBe(0);
    expect(stale).toHaveBeenCalledTimes(1);
  });

  it('after a few seconds the confirmation goes back to the sizes', () => {
    vi.useFakeTimers();
    const { f, el } = render();
    chip(el, '2Y').click();
    f.detectChanges();
    expect(el.querySelector('[role="status"]')).not.toBeNull();
    vi.advanceTimersByTime(4100);
    f.detectChanges();
    expect(el.querySelector('.rp-qa__sizes')).not.toBeNull();
  });

  it('CRD-11 Close tells the card to close the bar', () => {
    const { f, el } = render();
    const closed = vi.fn();
    f.componentInstance.closed.subscribe(closed);
    el.querySelector<HTMLButtonElement>('button[aria-label="Close quick add"]')!.click();
    expect(closed).toHaveBeenCalledTimes(1);
  });
});
