import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { ListingTile } from '../../models/listing.model';
import { ProductTileComponent } from './product-tile.component';

const tile = (extra: Partial<ListingTile> = {}): ListingTile => ({
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
  TestBed.configureTestingModule({ providers: [provideRouter([])] });
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

  it('PR-9 the tile has no add-to-bag control until the cart exists', () => {
    const el = render(tile({ sizes: [{ code: '3-4y', name: '3-4Y', soldOut: false }] }));

    expect(el.querySelector('button')).toBeNull();
    expect(el.querySelectorAll('a')).toHaveLength(1);
  });
});
