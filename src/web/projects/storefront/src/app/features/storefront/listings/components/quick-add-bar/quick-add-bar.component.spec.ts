import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { describe, expect, it, vi } from 'vitest';
import { ListingTileSize } from '../../models/listing.model';
import { QuickAddBarComponent, QuickAddState } from './quick-add-bar.component';

const size = (code: string, over: Partial<ListingTileSize> = {}): ListingTileSize => ({ code, name: code.toUpperCase(), soldOut: false, variantId: 100 + code.length, availability: 'InStock', colourName: 'Rose', ...over });

interface Setup {
  sizes?: ListingTileSize[];
  state?: QuickAddState;
  busy?: boolean;
  added?: string | null;
  notice?: string | null;
}

function render(s: Setup = {}) {
  TestBed.configureTestingModule({ providers: [provideRouter([])] });
  const f = TestBed.createComponent(QuickAddBarComponent);
  f.componentRef.setInput('sizes', s.sizes ?? [size('2y', { variantId: 1 }), size('3y', { variantId: 2 }), size('4y', { variantId: 3 })]);
  f.componentRef.setInput('productPath', '/girls/dresses/floral');
  f.componentRef.setInput('state', s.state ?? 'choosing');
  f.componentRef.setInput('busy', s.busy ?? false);
  f.componentRef.setInput('added', s.added ?? null);
  f.componentRef.setInput('notice', s.notice ?? null);
  f.detectChanges();
  const choose = vi.fn();
  const close = vi.fn();
  const viewBag = vi.fn();
  f.componentInstance.choose.subscribe(choose);
  f.componentInstance.close.subscribe(close);
  f.componentInstance.viewBag.subscribe(viewBag);
  return { el: f.nativeElement as HTMLElement, choose, close, viewBag };
}
const chips = (el: HTMLElement) => [...el.querySelectorAll<HTMLButtonElement>('.rp-qa__sizes button')];

describe('QuickAddBarComponent', () => {
  it('CRD-4 offers "Choose a size" and one 44px chip per size, in a list', () => {
    const { el } = render();
    expect(el.textContent).toContain('Choose a size');
    expect(el.querySelectorAll('ul.rp-qa__sizes > li').length).toBe(3);
    expect(chips(el).map((b) => b.textContent!.trim())).toEqual(['2Y', '3Y', '4Y']);
    expect(chips(el)[0].className).toContain('rp-size');
  });

  it("CRD-5 pressing an available size emits that size's variant and name, once", () => {
    const { el, choose } = render();
    chips(el)[1].click();
    expect(choose).toHaveBeenCalledTimes(1);
    expect(choose).toHaveBeenCalledWith({ variantId: 2, sizeName: '3Y', colourName: 'Rose' });
  });

  it('CRD-4 a sold-out size stays in the row, struck through, aria-disabled, with "sold out" in its name, and does nothing when pressed', () => {
    const { el, choose } = render({ sizes: [size('2y', { variantId: 1 }), size('3y', { variantId: 2, soldOut: true, availability: 'SoldOut' })] });
    const sold = chips(el)[1];
    expect(sold.getAttribute('aria-disabled')).toBe('true');
    expect(sold.querySelector('s')).not.toBeNull();
    expect(sold.textContent).toContain('sold out');
    sold.click();
    expect(choose).not.toHaveBeenCalled();
  });

  it('CRD-5 a size with no variant (an older response) cannot be added', () => {
    const { el, choose } = render({ sizes: [size('2y', { variantId: undefined }), size('3y', { variantId: 2 })] });
    expect(chips(el)[0].getAttribute('aria-disabled')).toBe('true');
    chips(el)[0].click();
    expect(choose).not.toHaveBeenCalled();
  });

  it('CRD-4 chips are disabled while the bag is busy so one press adds one line', () => {
    const { el, choose } = render({ busy: true });
    chips(el)[0].click();
    expect(choose).not.toHaveBeenCalled();
    expect(chips(el)[0].getAttribute('aria-disabled')).toBe('true');
  });

  it('CRD-7 when every size is sold out it says so and offers Notify me, a link to the product page', () => {
    const { el } = render({ sizes: [size('2y', { variantId: 1, soldOut: true, availability: 'SoldOut' }), size('3y', { variantId: 2, soldOut: true, availability: 'SoldOut' })] });
    expect(el.textContent).toContain('Sold out');
    const link = el.querySelector<HTMLAnchorElement>('a')!;
    expect(link.textContent).toContain('Notify me');
    expect(link.getAttribute('href')).toBe('/girls/dresses/floral?size=2y');
    expect(chips(el).length).toBe(0);
  });

  it('CRD-8 a product with one size shows Add to bag directly, with no chips', () => {
    const { el, choose } = render({ sizes: [size('onesize', { variantId: 9 })] });
    expect(chips(el).length).toBe(0);
    const button = [...el.querySelectorAll('button')].find((b) => b.textContent!.trim() === 'Add to bag')!;
    button.click();
    expect(choose).toHaveBeenCalledWith({ variantId: 9, sizeName: 'ONESIZE', colourName: 'Rose' });
  });

  it('CRD-5 after an add it says what was added and offers View bag', () => {
    const { el, viewBag } = render({ state: 'added', added: 'Added, size 3Y, Rose' });
    expect(el.querySelector('[role="status"]')!.textContent).toContain('Added, size 3Y, Rose');
    [...el.querySelectorAll('button')].find((b) => b.textContent!.trim() === 'View bag')!.click();
    expect(viewBag).toHaveBeenCalledTimes(1);
  });

  it('CRD-9 a refusal is shown in words and the chips stay so another size can be tried', () => {
    const { el } = render({ notice: 'That size has just sold out.' });
    expect(el.querySelector('[role="alert"]')!.textContent).toContain('just sold out');
    expect(chips(el).length).toBe(3);
  });

  it('CRD-11 the close button emits close', () => {
    const { el, close } = render();
    el.querySelector<HTMLButtonElement>('button[aria-label="Close quick add"]')!.click();
    expect(close).toHaveBeenCalledTimes(1);
  });
});
