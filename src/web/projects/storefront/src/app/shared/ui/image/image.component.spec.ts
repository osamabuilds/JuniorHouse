import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ImageAsset } from '../../models/image-asset.model';
import { ImageComponent } from './image.component';
import { provideRpImageLoader, RP_IMAGE_WIDTHS, rpImageLoader } from './image-loader';

const BLUR = 'data:image/jpeg;base64,/9j/4AAQSkZJRg==';
const img = (over: Partial<ImageAsset> = {}): ImageAsset => ({
  src: '/placeholders/front.jpg',
  width: 1080,
  height: 1350,
  alt: 'Linen shirt dress, front view',
  blurDataUrl: BLUR,
  ...over,
});

@Component({
  imports: [ImageComponent],
  template: `<rp-image
    [image]="image()"
    [second]="second()"
    [priority]="priority()"
    [decorative]="decorative()"
  />`,
})
class HostComponent {
  image = signal<ImageAsset | null>(img());
  second = signal<ImageAsset | null>(null);
  priority = signal(false);
  decorative = signal(false);
}

function setup(before?: (h: HostComponent) => void) {
  TestBed.configureTestingModule({ providers: [provideRpImageLoader()] });
  const f = TestBed.createComponent(HostComponent);
  before?.(f.componentInstance);
  f.detectChanges();
  const el = f.nativeElement as HTMLElement;
  return { f, el, imgs: () => Array.from(el.querySelectorAll('img')) };
}

describe('rp-image', () => {
  it('DSF-8 renders a 4:5 box with explicit size, alt and a six-width srcset', () => {
    const { el, imgs } = setup();
    const [first] = imgs();
    expect(el.querySelector('.rp-media')).not.toBeNull();
    expect(first.getAttribute('width')).toBe('1080');
    expect(first.getAttribute('height')).toBe('1350');
    expect(first.getAttribute('alt')).toBe('Linen shirt dress, front view');
    const widths = first.getAttribute('srcset')!.split(',').map((s) => s.trim().split(' ')[1]);
    expect(widths).toEqual(RP_IMAGE_WIDTHS.map((w) => `${w}w`));
  });

  it('DSF-8 builds derivative URLs through the single loader', () => {
    expect(rpImageLoader({ src: '/a.jpg', width: 640 })).toBe('/a.jpg?w=640');
  });

  it('DSF-9 is lazy and async by default', () => {
    const [first] = setup().imgs();
    expect(first.getAttribute('loading')).toBe('lazy');
    expect(first.getAttribute('fetchpriority')).not.toBe('high');
  });

  it('DSF-9 is eager with high fetch priority when it is the LCP image', () => {
    const { imgs } = setup((h) => h.priority.set(true));
    const [first] = imgs();
    expect(first.getAttribute('fetchpriority')).toBe('high');
    expect(first.getAttribute('loading')).toBe('eager');
  });

  it('DSF-10 shows the placeholder in the same box when there is no image', () => {
    const { f, el } = setup();
    f.componentInstance.image.set(null);
    f.detectChanges();
    expect(el.querySelector('img')).toBeNull();
    expect(el.querySelector('.rp-media > .rp-ph')).not.toBeNull();
  });

  it('DSF-10 falls back to the placeholder when the file fails to load', () => {
    const { f, el, imgs } = setup();
    imgs()[0].dispatchEvent(new Event('error'));
    f.detectChanges();
    expect(el.querySelector('img')).toBeNull();
    expect(el.querySelector('.rp-ph')).not.toBeNull();
  });

  it('DSF-10 ignores a blur placeholder that is not an image data URL', () => {
    const { f, imgs } = setup();
    f.componentInstance.image.set(img({ blurDataUrl: 'javascript:alert(1)' }));
    f.detectChanges();
    expect(imgs()[0].getAttribute('style') ?? '').not.toContain('javascript');
  });

  it('DSF-8 needs alt text unless the image is marked decorative', () => {
    const { f } = setup();
    f.componentInstance.image.set(img({ alt: '' }));
    expect(() => f.detectChanges()).toThrow(/alt text/);
  });

  it('DSF-8 gives a decorative image an empty alt', () => {
    const { f, imgs } = setup();
    f.componentInstance.image.set(img({ alt: '' }));
    f.componentInstance.decorative.set(true);
    f.detectChanges();
    expect(imgs()[0].getAttribute('alt')).toBe('');
  });

  it('DSF-11 does not request the second view until a mouse pointer enters', () => {
    const { f, el, imgs } = setup();
    f.componentInstance.second.set(img({ src: '/placeholders/back.jpg', alt: 'back' }));
    f.detectChanges();
    expect(imgs().length).toBe(1);

    const host = el.querySelector('rp-image')!;
    host.dispatchEvent(new PointerEvent('pointerenter', { pointerType: 'touch' }));
    f.detectChanges();
    expect(imgs().length).toBe(1);

    host.dispatchEvent(new PointerEvent('pointerenter', { pointerType: 'mouse' }));
    f.detectChanges();
    expect(imgs().length).toBe(2);
    expect(imgs()[1].classList.contains('rp-image__second')).toBe(true);
  });

  it('DSF-11 also requests the second view on first keyboard focus', () => {
    const { f, el, imgs } = setup();
    f.componentInstance.second.set(img({ src: '/placeholders/back.jpg', alt: 'back' }));
    f.detectChanges();
    el.querySelector('rp-image')!.dispatchEvent(new FocusEvent('focusin'));
    f.detectChanges();
    expect(imgs().length).toBe(2);
  });
});
