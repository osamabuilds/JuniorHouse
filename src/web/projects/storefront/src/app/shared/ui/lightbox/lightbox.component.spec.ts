import { ChangeDetectionStrategy, Component, viewChild } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { polyfillDialog } from '../../../layout/dialog/dialog.testing';
import { LightboxComponent, LightboxImage } from './lightbox.component';

const images: LightboxImage[] = [1, 2, 3].map((n) => ({ alt: `View ${n}`, width: 800, height: 1000, src: `/media/${n}-1440.webp`, fallback: `/media/${n}-1440.jpg` }));

@Component({
  imports: [LightboxComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<button id="opener" type="button">open</button><rp-lightbox [images]="images" />`,
})
class Host {
  readonly images = images;
  readonly box = viewChild.required(LightboxComponent);
}

function setup(): { f: ComponentFixture<Host>; el: HTMLElement; lb: LightboxComponent } {
  polyfillDialog();
  const f = TestBed.createComponent(Host);
  f.detectChanges();
  const el = f.nativeElement as HTMLElement;
  el.querySelector<HTMLElement>('#opener')!.focus();
  return { f, el, lb: f.componentInstance.box() };
}
const q = (el: HTMLElement, s: string) => el.querySelector<HTMLElement>(s);
const frame = (el: HTMLElement) => q(el, '.rp-lb__img')!;
const key = (el: HTMLElement, k: string) => q(el, 'dialog')!.dispatchEvent(new KeyboardEvent('keydown', { key: k, bubbles: true }));
const scale = (el: HTMLElement) => Number(/scale\(([\d.]+)\)/.exec(frame(el).style.transform)?.[1] ?? 1);

function mockBox(el: HTMLElement): void {
  frame(el).getBoundingClientRect = () => ({ x: 0, y: 0, left: 0, top: 0, right: 400, bottom: 500, width: 400, height: 500, toJSON: () => ({}) });
}
function pointer(el: HTMLElement, type: string, init: PointerEventInit & { pointerId: number }): void {
  const ev = new Event(type, { bubbles: true, cancelable: true }) as Event & Record<string, unknown>;
  Object.assign(ev, { pointerId: init.pointerId, clientX: init.clientX ?? 0, clientY: init.clientY ?? 0, pointerType: init.pointerType ?? 'mouse', button: 0 });
  frame(el).dispatchEvent(ev);
}

describe('LightboxComponent', () => {
  beforeEach(() => {
    HTMLElement.prototype.setPointerCapture = () => undefined;
    HTMLElement.prototype.releasePointerCapture = () => undefined;
  });

  it('GAL-9 no picture exists before the viewer opens, and exactly one after', () => {
    const { f, el, lb } = setup();
    expect(el.querySelectorAll('.rp-lb__img img').length).toBe(0);
    lb.open(1);
    f.detectChanges();
    const imgs = el.querySelectorAll('.rp-lb__img img');
    expect(imgs.length).toBe(1);
    expect((imgs[0] as HTMLImageElement).getAttribute('src')).toContain('2-1440');
    expect((imgs[0] as HTMLImageElement).alt).toBe('View 2');
  });

  it('GAL-3 opens modally at the picture with a counter, a close button and the hint', () => {
    const { f, el, lb } = setup();
    lb.open(1);
    f.detectChanges();
    expect(q(el, 'dialog')!.hasAttribute('open')).toBe(true);
    expect(q(el, '.rp-lb__top')!.textContent).toContain('2 / 3');
    expect(q(el, 'button[aria-label="Close"]')).not.toBeNull();
    expect(q(el, '.rp-lb__hint')!.textContent).toContain('double-tap');
  });

  it('GAL-6 the previous and next buttons and the Left and Right keys move, with no wrap', () => {
    const { f, el, lb } = setup();
    lb.open(0);
    f.detectChanges();
    expect(q(el, '[aria-label="Previous picture"]')!.hasAttribute('disabled')).toBe(true);
    q(el, '[aria-label="Next picture"]')!.click();
    f.detectChanges();
    expect(q(el, '.rp-lb__top')!.textContent).toContain('2 / 3');
    key(el, 'ArrowRight');
    f.detectChanges();
    expect(q(el, '.rp-lb__top')!.textContent).toContain('3 / 3');
    expect(q(el, '[aria-label="Next picture"]')!.hasAttribute('disabled')).toBe(true);
    key(el, 'ArrowRight');
    f.detectChanges();
    expect(q(el, '.rp-lb__top')!.textContent).toContain('3 / 3');
    key(el, 'ArrowLeft');
    f.detectChanges();
    expect(q(el, '.rp-lb__top')!.textContent).toContain('2 / 3');
  });

  it('GAL-4 a mouse click toggles 2x about the pointer and a second click returns to 1x', () => {
    const { f, el, lb } = setup();
    lb.open(0);
    f.detectChanges();
    mockBox(el);
    pointer(el, 'pointerdown', { pointerId: 1, clientX: 300, clientY: 250 });
    pointer(el, 'pointerup', { pointerId: 1, clientX: 300, clientY: 250 });
    f.detectChanges();
    expect(scale(el)).toBe(2);
    pointer(el, 'pointerdown', { pointerId: 1, clientX: 300, clientY: 250 });
    pointer(el, 'pointerup', { pointerId: 1, clientX: 300, clientY: 250 });
    f.detectChanges();
    expect(scale(el)).toBe(1);
  });

  it('GAL-4 on touch a single tap does nothing and a double-tap toggles', () => {
    const { f, el, lb } = setup();
    lb.open(0);
    f.detectChanges();
    mockBox(el);
    const tap = () => {
      pointer(el, 'pointerdown', { pointerId: 2, clientX: 200, clientY: 250, pointerType: 'touch' });
      pointer(el, 'pointerup', { pointerId: 2, clientX: 200, clientY: 250, pointerType: 'touch' });
    };
    tap();
    f.detectChanges();
    expect(scale(el)).toBe(1);
    tap();
    f.detectChanges();
    expect(scale(el)).toBe(2);
  });

  it('GAL-4 GAL-7 the + key toggles 2x, the - key and the buttons step, and the buttons match the keys', () => {
    const { f, el, lb } = setup();
    lb.open(0);
    f.detectChanges();
    mockBox(el);
    key(el, '+');
    f.detectChanges();
    expect(scale(el)).toBe(2);
    q(el, '[aria-label="Zoom out"]')!.click();
    f.detectChanges();
    expect(scale(el)).toBe(1.5);
    q(el, '[aria-label="Zoom in"]')!.click();
    f.detectChanges();
    expect(scale(el)).toBe(2);
    key(el, '-');
    f.detectChanges();
    expect(scale(el)).toBe(1.5);
  });

  it('GAL-5 two pointers pinch and the scale never goes beyond 4x', () => {
    const { f, el, lb } = setup();
    lb.open(0);
    f.detectChanges();
    mockBox(el);
    pointer(el, 'pointerdown', { pointerId: 1, clientX: 180, clientY: 250, pointerType: 'touch' });
    pointer(el, 'pointerdown', { pointerId: 2, clientX: 220, clientY: 250, pointerType: 'touch' });
    pointer(el, 'pointermove', { pointerId: 2, clientX: 220 + 4000, clientY: 250, pointerType: 'touch' });
    f.detectChanges();
    expect(scale(el)).toBe(4);
  });

  it('GAL-6 swiping moves between pictures at 1x but does nothing while zoomed', () => {
    const { f, el, lb } = setup();
    lb.open(0);
    f.detectChanges();
    mockBox(el);
    const swipe = (from: number, to: number) => {
      pointer(el, 'pointerdown', { pointerId: 3, clientX: from, clientY: 250, pointerType: 'touch' });
      pointer(el, 'pointermove', { pointerId: 3, clientX: to, clientY: 250, pointerType: 'touch' });
      pointer(el, 'pointerup', { pointerId: 3, clientX: to, clientY: 250, pointerType: 'touch' });
      f.detectChanges();
    };
    swipe(300, 100);
    expect(q(el, '.rp-lb__top')!.textContent).toContain('2 / 3');
    key(el, '+');
    f.detectChanges();
    swipe(300, 100);
    expect(q(el, '.rp-lb__top')!.textContent).toContain('2 / 3');
  });

  it('GAL-7 when zoomed the arrow keys pan instead of moving to the next picture', () => {
    const { f, el, lb } = setup();
    lb.open(0);
    f.detectChanges();
    mockBox(el);
    key(el, '+');
    f.detectChanges();
    key(el, 'ArrowRight');
    f.detectChanges();
    expect(q(el, '.rp-lb__top')!.textContent).toContain('1 / 3');
    expect(frame(el).style.transform).toContain('translate(-');
  });

  it('GAL-8 Escape closes, the picture is removed and focus returns to the opener', () => {
    const { f, el, lb } = setup();
    const opener = q(el, '#opener')!;
    lb.open(0);
    f.detectChanges();
    q(el, 'dialog')!.dispatchEvent(new Event('cancel', { cancelable: true }));
    f.detectChanges();
    expect(q(el, 'dialog')!.hasAttribute('open')).toBe(false);
    expect(el.querySelectorAll('.rp-lb__img img').length).toBe(0);
    expect(document.activeElement).toBe(opener);
    expect(document.body.classList.contains('rp-no-scroll')).toBe(false);
  });

  it('GAL-7 the reset button returns the picture to 1x', () => {
    const { f, el, lb } = setup();
    lb.open(0);
    f.detectChanges();
    mockBox(el);
    key(el, '+');
    f.detectChanges();
    q(el, '[aria-label="Reset zoom"]')!.click();
    f.detectChanges();
    expect(scale(el)).toBe(1);
  });
});
