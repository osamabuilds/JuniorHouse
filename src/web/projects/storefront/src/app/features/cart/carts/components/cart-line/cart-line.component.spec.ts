import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { CartLine, LineIssue } from '../../models/cart.model';
import { CartLineComponent } from './cart-line.component';

const base: CartLine = {
  variantId: 5, productId: 1, name: 'Floral Dress', path: '/girls/dresses/floral-dress', colour: 'Red', size: '2-3Y', sku: 'S5', unitPrice: 1250, compareAtPrice: null,
  quantity: 2, lineTotal: 2500, available: 9, previousPrice: null, issues: [], imageUrl: null, imageFallbackUrl: null, imageAlt: 'A red dress',
};

function render(over: Partial<CartLine> = {}, busy = false) {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({ providers: [provideRouter([])] });
  const fixture = TestBed.createComponent(CartLineComponent);
  fixture.componentRef.setInput('line', { ...base, ...over });
  fixture.componentRef.setInput('busy', busy);
  fixture.detectChanges();
  return { fixture, el: fixture.nativeElement as HTMLElement };
}

const issues = (...i: LineIssue[]): LineIssue[] => i;

describe('CartLineComponent', () => {
  it('CRT-1 shows the name as a link, the meta, each price and the line total', () => {
    const { el } = render();

    expect(el.querySelector('a')!.getAttribute('href')).toBe('/girls/dresses/floral-dress');
    expect(el.textContent).toContain('Floral Dress');
    expect(el.textContent).toContain('Red');
    expect(el.textContent).toContain('Size 2-3Y');
    expect(el.textContent).toContain('Rs. 1,250 each');
    expect(el.querySelector('.rp-lineprice')!.textContent).toContain('Rs. 2,500');
  });

  it('CRT-1 shows the real product picture, WebP with a JPEG fallback, named by its alt text', () => {
    const { el } = render({ imageUrl: '/media/a-320.webp', imageFallbackUrl: '/media/a-320.jpg', imageAlt: 'A red floral dress' });

    expect(el.querySelector('picture source')!.getAttribute('srcset')).toBe('/media/a-320.webp');
    const img = el.querySelector('picture img') as HTMLImageElement;
    expect(img.getAttribute('src')).toBe('/media/a-320.jpg');
    expect(img.getAttribute('alt')).toBe('A red floral dress');
    expect(el.querySelector('.rp-ph')).toBeNull();
  });

  it('Remove is a trash icon with an accessible name that says which item', () => {
    const { el } = render();
    const remove = el.querySelector('.rp-line__remove') as HTMLButtonElement;

    expect(remove.getAttribute('aria-label')).toBe('Remove Floral Dress');
    expect(remove.querySelector('svg')).not.toBeNull();
    expect(remove.textContent?.trim()).toBe('');
  });

  it('the picture is named for assistive technology', () => {
    const { el } = render();

    expect(el.querySelector('.rp-ph')!.getAttribute('aria-label')).toBe('A red dress');
  });

  it('a discounted line shows the struck full price, with "Was" for screen readers', () => {
    const { el } = render({ compareAtPrice: 1500 });

    expect(el.querySelector('s')!.textContent).toContain('Rs. 3,000');
    expect(el.querySelector('s .sr-only')!.textContent).toContain('Was');
  });

  it('CRT-7 a line short of stock says so in words, with the count', () => {
    const { el } = render({ issues: issues('low-stock'), available: 1 });

    expect(el.querySelector('.rp-bag-note')!.textContent).toContain('Only 1 in stock');
  });

  it('CRT-7 a line with no stock says it is out of stock', () => {
    const { el } = render({ issues: issues('low-stock'), available: 0 });

    expect(el.querySelector('.rp-bag-note')!.textContent).toContain('Out of stock');
  });

  it('CRT-5 an unavailable line says so, offers no stepper, and can still be removed', () => {
    const { el } = render({ issues: issues('unavailable'), unitPrice: null, lineTotal: 0 });

    expect(el.querySelector('.rp-bag-note')!.textContent).toContain('no longer available');
    expect(el.querySelector('rp-quantity-stepper')).toBeNull();
    expect([...el.querySelectorAll('button')].some((b) => b.getAttribute('aria-label')?.startsWith('Remove'))).toBe(true);
  });

  it('CRT-8 a changed price is pointed out with the old and the new figure', () => {
    const { el } = render({ issues: issues('price-changed'), previousPrice: 1000, unitPrice: 1250 });

    const note = el.querySelector('.rp-bag-note')!.textContent!;
    expect(note).toContain('1,000');
    expect(note).toContain('1,250');
  });

  it('the stepper and Remove send the new quantity and the removal, and wait while busy', () => {
    const { fixture, el } = render();
    const changes: number[] = [];
    let removed = 0;
    fixture.componentInstance.quantityChange.subscribe((q) => changes.push(q));
    fixture.componentInstance.remove.subscribe(() => removed++);

    (el.querySelector('button[aria-label="Increase quantity"]') as HTMLButtonElement).click();
    (el.querySelector('button[aria-label="Decrease quantity"]') as HTMLButtonElement).click();
    [...el.querySelectorAll('button')].find((b) => b.getAttribute('aria-label')?.startsWith('Remove'))!.click();

    expect(changes).toEqual([3, 1]);
    expect(removed).toBe(1);

    const busy = render({}, true);
    expect([...busy.el.querySelectorAll('button')].every((b) => b.disabled)).toBe(true);
  });

  it('the stepper never goes below one or above the limit', () => {
    const one = render({ quantity: 1 });
    expect((one.el.querySelector('button[aria-label="Decrease quantity"]') as HTMLButtonElement).disabled).toBe(true);

    const ten = render({ quantity: 10 });
    expect((ten.el.querySelector('button[aria-label="Increase quantity"]') as HTMLButtonElement).disabled).toBe(true);
  });
});
