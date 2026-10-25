import { TestBed } from '@angular/core/testing';
import { ProductPrice } from '../../models/product-page.model';
import { ProductPriceComponent } from './product-price.component';

function render(price: ProductPrice | null): HTMLElement {
  const fixture = TestBed.createComponent(ProductPriceComponent);
  fixture.componentRef.setInput('price', price);
  fixture.detectChanges();
  return fixture.nativeElement as HTMLElement;
}

const sale: ProductPrice = { price: 1200, compareAtPrice: 1500, savingPercent: 20, savingAmount: 300, isFrom: false };

describe('ProductPriceComponent', () => {
  it('PR-1 a discount shows the large sale price, the struck old price, the minus pill and the saving in words', () => {
    const el = render(sale);

    expect(el.querySelector('.rp-price.rp-price--lg.rp-price--sale')?.textContent).toContain('Rs. 1,200');
    expect(el.querySelector('s')?.textContent).toContain('Rs. 1,500');
    expect(el.querySelector('.rp-price .sr-only')?.textContent).toContain('Now');
    expect(el.querySelector('s .sr-only')?.textContent).toContain('Was');
    expect(el.querySelector('.rp-off')?.textContent?.trim()).toBe('−20%');
    expect(el.querySelector('.rp-save')?.textContent?.trim()).toBe('You save Rs. 300');
  });

  it('PR-2 the saving is the server amount, never computed from the two prices', () => {
    expect(render({ ...sale, savingAmount: 275 }).querySelector('.rp-save')?.textContent?.trim()).toBe('You save Rs. 275');
  });

  it('PR-3 no discount means no strike, pill or saving line', () => {
    const el = render({ price: 1000, compareAtPrice: null, savingPercent: 0, savingAmount: 0, isFrom: false });

    expect(el.querySelector('s')).toBeNull();
    expect(el.querySelector('.rp-off')).toBeNull();
    expect(el.querySelector('.rp-save')).toBeNull();
    expect(el.querySelector('.rp-price--sale')).toBeNull();
  });

  it('PR-5 keeps "From" before a size is chosen', () => {
    expect(render({ ...sale, isFrom: true }).querySelector('.rp-price')?.textContent).toContain('From');
  });

  it('renders nothing when the product has no price', () => {
    expect(render(null).querySelector('.rp-price')).toBeNull();
  });
});
