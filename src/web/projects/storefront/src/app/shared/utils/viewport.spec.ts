import { afterEach, describe, expect, it, vi } from 'vitest';
import { watchHeight, watchInView, watchMedia } from './viewport';

class FakeObserver {
  static last: FakeObserver | null = null;
  disconnected = false;
  observed: Element[] = [];
  constructor(public cb: (entries: unknown[]) => void) {
    FakeObserver.last = this;
  }
  observe(el: Element): void {
    this.observed.push(el);
  }
  disconnect(): void {
    this.disconnected = true;
  }
  unobserve(): void {}
}

afterEach(() => {
  vi.unstubAllGlobals();
  FakeObserver.last = null;
});

describe('viewport helpers', () => {
  it('BUY-2 watchInView reports whether the element is in the viewport and disconnects when stopped', () => {
    vi.stubGlobal('IntersectionObserver', FakeObserver);
    const seen: boolean[] = [];
    const stop = watchInView(document.createElement('div'), (v) => seen.push(v));
    FakeObserver.last!.cb([{ isIntersecting: false }]);
    FakeObserver.last!.cb([{ isIntersecting: true }]);
    expect(seen).toEqual([false, true]);
    stop();
    expect(FakeObserver.last!.disconnected).toBe(true);
  });

  it('BUY-2 without IntersectionObserver (the server, an old browser) nothing is observed and the element counts as visible', () => {
    vi.stubGlobal('IntersectionObserver', undefined);
    const seen: boolean[] = [];
    const stop = watchInView(document.createElement('div'), (v) => seen.push(v));
    expect(seen).toEqual([true]);
    expect(() => stop()).not.toThrow();
  });

  it('BUY-9 watchMedia reports the current match and every change, and stops listening when stopped', () => {
    let listener: ((e: { matches: boolean }) => void) | null = null;
    const removed = vi.fn();
    vi.stubGlobal('matchMedia', () => ({ matches: true, addEventListener: (_: string, l: typeof listener) => (listener = l), removeEventListener: removed }));
    const seen: boolean[] = [];
    const stop = watchMedia('(max-width: 63.99rem)', (m) => seen.push(m));
    listener!({ matches: false });
    expect(seen).toEqual([true, false]);
    stop();
    expect(removed).toHaveBeenCalled();
  });

  it('BUY-7 watchHeight reports the element height now and when it changes', () => {
    vi.stubGlobal('ResizeObserver', FakeObserver);
    const el = document.createElement('div');
    el.getBoundingClientRect = () => ({ height: 72 }) as DOMRect;
    const seen: number[] = [];
    const stop = watchHeight(el, (h) => seen.push(h));
    expect(seen).toEqual([72]);
    el.getBoundingClientRect = () => ({ height: 96 }) as DOMRect;
    FakeObserver.last!.cb([{}]);
    expect(seen).toEqual([72, 96]);
    stop();
    expect(FakeObserver.last!.disconnected).toBe(true);
  });
});
