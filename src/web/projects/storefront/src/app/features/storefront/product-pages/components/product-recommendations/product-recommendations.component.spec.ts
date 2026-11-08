import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideEffects } from '@ngrx/effects';
import { provideStore } from '@ngrx/store';
import { describe, expect, it, vi } from 'vitest';
import { provideCart } from '../../../../cart/carts/cart.providers';
import { ListingTile } from '../../../listings/models/listing.model';
import { ProductRecommendationsComponent } from './product-recommendations.component';

const tile = (id: number): ListingTile => ({
  productId: id,
  path: `/girls/dresses/p-${id}`,
  name: `Dress ${id}`,
  price: 800,
  compareAtPrice: null,
  savingPercent: 0,
  savingAmount: 0,
  colourCount: 1,
  hasSoldOutSizes: false,
  sizes: [{ code: '2y', name: '2Y', soldOut: false, variantId: id * 10, availability: 'InStock', colourName: 'Rose' }],
  image: null,
});

function render(tiles: readonly ListingTile[]) {
  TestBed.configureTestingModule({ providers: [provideRouter([{ path: '**', children: [] }]), provideStore(), provideEffects(), provideCart()] });
  const f = TestBed.createComponent(ProductRecommendationsComponent);
  f.componentRef.setInput('headingId', 'rec-h');
  f.componentRef.setInput('heading', 'You may also like');
  f.componentRef.setInput('eyebrow', 'More to love');
  f.componentRef.setInput('tiles', tiles);
  f.detectChanges();
  return { f, el: f.nativeElement as HTMLElement };
}

describe('ProductRecommendationsComponent', () => {
  it('REC-9 the server markup is a labelled section with one h2 and one real link per card', () => {
    const { el } = render([tile(1), tile(2), tile(3)]);

    const section = el.querySelector('section')!;
    expect(section.getAttribute('aria-labelledby')).toBe('rec-h');
    expect(el.querySelectorAll('h2').length).toBe(1);
    expect(el.querySelector('h2')!.id).toBe('rec-h');
    expect(el.querySelectorAll('h1').length).toBe(0);
    const hrefs = [...el.querySelectorAll<HTMLAnchorElement>('ul.rp-rail li h3 a')].map((a) => a.getAttribute('href'));
    expect(hrefs).toEqual(['/girls/dresses/p-1', '/girls/dresses/p-2', '/girls/dresses/p-3']);
  });

  it('REC-10 renders nothing, not even a heading, for an empty row', () => {
    const { el } = render([]);

    expect(el.querySelector('section')).toBeNull();
    expect(el.querySelector('h2')).toBeNull();
    expect(el.textContent!.trim()).toBe('');
  });

  it('REC-14 the list is focusable and named, and the arrows are buttons with names', () => {
    const { el } = render([tile(1), tile(2)]);

    const list = el.querySelector('ul.rp-rail')!;
    expect(list.getAttribute('tabindex')).toBe('0');
    expect(list.getAttribute('aria-label')).toBe('You may also like');
    const arrows = [...el.querySelectorAll<HTMLButtonElement>('.rp-arrows button')];
    expect(arrows.map((b) => [b.type, b.getAttribute('aria-label')])).toEqual([
      ['button', 'Previous'],
      ['button', 'Next'],
    ]);
  });

  it('REC-14 the arrows scroll the list by one card', () => {
    const { el } = render([tile(1), tile(2)]);
    const list = el.querySelector<HTMLElement>('ul.rp-rail')!;
    list.scrollBy = vi.fn();
    vi.spyOn(el.querySelector('li')!, 'getBoundingClientRect').mockReturnValue({ width: 200 } as DOMRect);

    el.querySelectorAll<HTMLButtonElement>('.rp-arrows button')[1].click();
    el.querySelectorAll<HTMLButtonElement>('.rp-arrows button')[0].click();

    const moves = (list.scrollBy as ReturnType<typeof vi.fn>).mock.calls.map(([o]) => (o as { left: number }).left);
    expect(moves).toEqual([200, -200]);
  });

  it('REC-12 using a card link says which product was opened', async () => {
    const { f, el } = render([tile(1), tile(2)]);
    const opened = vi.fn();
    f.componentInstance.opened.subscribe(opened);

    el.querySelectorAll<HTMLAnchorElement>('li h3 a')[1].dispatchEvent(new MouseEvent('click', { bubbles: true }));
    el.querySelector<HTMLElement>('ul.rp-rail')!.dispatchEvent(new MouseEvent('click', { bubbles: true }));

    await f.whenStable();
    expect(opened.mock.calls).toEqual([[2]]);
  });
});
