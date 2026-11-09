import { TestBed } from '@angular/core/testing';
import { Perk } from '../../utils/perks.util';
import { ProductPerksComponent } from './product-perks.component';

function render(perks: readonly Perk[]): HTMLElement {
  const f = TestBed.createComponent(ProductPerksComponent);
  f.componentRef.setInput('perks', perks);
  f.detectChanges();
  return f.nativeElement as HTMLElement;
}

describe('ProductPerksComponent', () => {
  it('BUY-10 renders a list with one item per perk, and WhatsApp as a real link', () => {
    const el = render([
      { icon: 'truck', text: 'Delivery in 2-4 days' },
      { icon: 'chat', text: 'Questions? Message us on WhatsApp', href: 'https://wa.me/923315613217' },
    ]);
    expect(el.querySelectorAll('ul.rp-perks > li').length).toBe(2);
    const link = el.querySelector<HTMLAnchorElement>('li a')!;
    expect(link.getAttribute('href')).toBe('https://wa.me/923315613217');
    expect(link.textContent).toContain('WhatsApp');
    expect(el.querySelector('li:first-child a')).toBeNull();
  });

  it('BUY-11 renders nothing at all when there are no perks', () => {
    const el = render([]);
    expect(el.querySelector('ul')).toBeNull();
    expect(el.textContent!.trim()).toBe('');
  });
});
