import { ComponentFixture, TestBed } from '@angular/core/testing';
import { polyfillDialog } from '../../../../../layout/dialog/dialog.testing';
import { ProductImage } from '../../models/product-page.model';
import { ProductGalleryComponent } from './product-gallery.component';

const image = (n: number): ProductImage => ({
  altText: `Dress, view ${n}`,
  width: 800,
  height: 1000,
  sources: [
    { width: 640, webp: `/media/${n}-640.webp`, jpeg: `/media/${n}-640.jpg` },
    { width: 1440, webp: `/media/${n}-1440.webp`, jpeg: `/media/${n}-1440.jpg` },
  ],
});

function render(images: ProductImage[]): { f: ComponentFixture<ProductGalleryComponent>; el: HTMLElement } {
  polyfillDialog();
  const f = TestBed.createComponent(ProductGalleryComponent);
  f.componentRef.setInput('images', images);
  f.detectChanges();
  return { f, el: f.nativeElement as HTMLElement };
}
const buttons = (el: HTMLElement) => [...el.querySelectorAll<HTMLButtonElement>('.rp-gallery__list button.rp-gimg')];

describe('ProductGalleryComponent', () => {
  it('GAL-1 every picture is an img inside a button, the first eager and high priority, the rest lazy, all with alt and size', () => {
    const { el } = render([1, 2, 3].map(image));
    const list = buttons(el);
    expect(list.length).toBe(3);
    const imgs = list.map((b) => b.querySelector('img')!);
    expect(imgs.map((i) => i.getAttribute('loading'))).toEqual(['eager', 'lazy', 'lazy']);
    expect(imgs[0].getAttribute('fetchpriority')).toBe('high');
    expect(imgs[1].getAttribute('fetchpriority')).toBeNull();
    for (const i of imgs) {
      expect(i.alt).not.toBe('');
      expect(i.getAttribute('width')).toBe('800');
      expect(i.getAttribute('height')).toBe('1000');
    }
    expect(list[1].getAttribute('type')).toBe('button');
    expect(list[1].getAttribute('aria-label')).toContain('2 of 3');
  });

  it('GAL-2 the counter starts at 1 / N, is polite, and follows the strip as it settles', () => {
    vi.useFakeTimers();
    const { f, el } = render([1, 2, 3].map(image));
    const count = el.querySelector<HTMLElement>('.rp-gallery__count')!;
    expect(count.textContent!.trim()).toBe('1 / 3');
    expect(count.getAttribute('aria-live')).toBe('polite');
    const list = el.querySelector<HTMLElement>('.rp-gallery__list')!;
    Object.defineProperty(list, 'clientWidth', { value: 390, configurable: true });
    list.scrollLeft = 400;
    list.dispatchEvent(new Event('scroll'));
    vi.advanceTimersByTime(200);
    f.detectChanges();
    expect(count.textContent!.trim()).toBe('2 / 3');
    vi.useRealTimers();
  });

  it('GAL-2 one picture has no counter', () => {
    const { el } = render([image(1)]);
    expect(el.querySelector('.rp-gallery__count')).toBeNull();
  });

  it('GAL-3 pressing a picture opens the viewer at that picture', () => {
    const { f, el } = render([1, 2, 3].map(image));
    buttons(el)[1].click();
    f.detectChanges();
    expect(el.querySelector('dialog')!.hasAttribute('open')).toBe(true);
    expect(el.querySelector('.rp-lb__top')!.textContent).toContain('2 / 3');
    const src = el.querySelector('.rp-lb__img img')!.getAttribute('src')!;
    expect(src).toContain('2-1440');
  });

  it('GAL-9 the viewer asks for no larger picture until it opens', () => {
    const { el } = render([1, 2].map(image));
    expect(el.querySelectorAll('.rp-lb__img img').length).toBe(0);
  });

  it('GAL-11 a different colour (different pictures) shows its pictures and resets the counter', () => {
    const { f, el } = render([1, 2, 3].map(image));
    f.componentRef.setInput('images', [7, 8].map(image));
    f.detectChanges();
    expect(buttons(el).length).toBe(2);
    expect(buttons(el)[0].querySelector('img')!.alt).toContain('view 7');
    expect(el.querySelector('.rp-gallery__count')!.textContent!.trim()).toBe('1 / 2');
  });

  it('GAL-12 no pictures shows the existing message and nothing opens', () => {
    const { el } = render([]);
    expect(el.textContent).toContain('Photographs of this piece are on their way.');
    expect(buttons(el).length).toBe(0);
  });
});
