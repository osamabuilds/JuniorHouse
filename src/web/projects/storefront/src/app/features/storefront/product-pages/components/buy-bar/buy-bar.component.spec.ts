import { TestBed } from '@angular/core/testing';
import { ProductPrice } from '../../models/product-page.model';
import { BuyBarComponent } from './buy-bar.component';

const plain: ProductPrice = { price: 1299, compareAtPrice: null, savingPercent: 0, savingAmount: 0, isFrom: false };
const sale: ProductPrice = { price: 899, compareAtPrice: 1200, savingPercent: 25, savingAmount: 301, isFrom: false };

function render(over: { price?: ProductPrice | null; busy?: boolean; hint?: string | null; message?: string | null } = {}) {
  const f = TestBed.createComponent(BuyBarComponent);
  f.componentRef.setInput('price', over.price === undefined ? plain : over.price);
  f.componentRef.setInput('busy', over.busy ?? false);
  f.componentRef.setInput('hint', over.hint ?? null);
  f.componentRef.setInput('message', over.message ?? null);
  f.detectChanges();
  const add = vi.fn();
  f.componentInstance.add.subscribe(add);
  return { el: f.nativeElement as HTMLElement, add };
}

describe('BuyBarComponent', () => {
  it('BUY-1 BUY-6 shows the server price and an Add to bag button', () => {
    const { el } = render();
    expect(el.querySelector('.rp-buybar__price')!.textContent).toContain('1,299');
    expect([...el.querySelectorAll('button')].map((b) => b.textContent!.trim())).toEqual(['Add to bag']);
    expect(el.querySelector('s')).toBeNull();
  });

  it('BUY-1 a discounted price shows the old price struck through and announced as "Was"', () => {
    const { el } = render({ price: sale });
    expect(el.querySelector('.rp-buybar__price')!.textContent).toContain('899');
    const old = el.querySelector('s')!;
    expect(old.textContent).toContain('1,200');
    expect(old.querySelector('.sr-only')!.textContent).toContain('Was');
  });

  it('BUY-3 pressing the button emits add, and the button is disabled while the bag is busy', () => {
    const idle = render();
    idle.el.querySelector('button')!.click();
    expect(idle.add).toHaveBeenCalledTimes(1);

    const busy = render({ busy: true });
    expect(busy.el.querySelector('button')!.disabled).toBe(true);
  });

  it('BUY-3 BUY-5 shows the hint and any bag message in words, and the copies are not announced a second time', () => {
    const { el } = render({ hint: 'Choose a size to add this to your bag.', message: 'That size has just sold out.' });
    expect(el.textContent).toContain('Choose a size');
    expect(el.textContent).toContain('just sold out');
    expect(el.querySelector('[aria-hidden="true"]')).not.toBeNull();
  });

  it('BUY-8 the button is at least 44px by using the shared button style, and the bar has no landmark role', () => {
    const { el } = render();
    expect(el.querySelector('button')!.className).toContain('rp-btn');
    expect(el.querySelector('[role="region"], [role="navigation"], nav, aside')).toBeNull();
  });
});
