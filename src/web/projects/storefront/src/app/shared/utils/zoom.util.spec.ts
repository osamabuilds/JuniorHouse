import { describe, expect, it } from 'vitest';
import { INITIAL_ZOOM, MAX_SCALE, clampPan, imageInView, panBy, pinchTo, stepZoom, toggleAt } from './zoom.util';

const box = { width: 400, height: 500 };

describe('zoom util', () => {
  it('GAL-4 toggling from 1x gives 2x with the pointed spot kept under the pointer, and toggling again resets', () => {
    // A point 100px right of and 50px below the centre of the picture.
    const zoomed = toggleAt(INITIAL_ZOOM, { x: 100, y: 50 }, box);
    expect(zoomed.scale).toBe(2);
    // The picture spot under the pointer stays under it: translate = -point * (scale - 1).
    expect(zoomed.x).toBe(-100);
    expect(zoomed.y).toBe(-50);
    expect(toggleAt(zoomed, { x: 0, y: 0 }, box)).toEqual(INITIAL_ZOOM);
  });

  it('GAL-4 toggling near an edge never shows empty space beside the picture', () => {
    const zoomed = toggleAt(INITIAL_ZOOM, { x: 200, y: 250 }, box);
    // At 2x the picture is 800 wide in a 400 box: it can move at most 200 either way.
    expect(Math.abs(zoomed.x)).toBeLessThanOrEqual(200);
    expect(Math.abs(zoomed.y)).toBeLessThanOrEqual(250);
  });

  it('GAL-5 pinch scales between 1x and 4x and never beyond', () => {
    const big = pinchTo(INITIAL_ZOOM, 1, 100, 10_000, { x: 0, y: 0 }, box);
    expect(big.scale).toBe(MAX_SCALE);
    const small = pinchTo({ scale: 3, x: 0, y: 0 }, 3, 100, 1, { x: 0, y: 0 }, box);
    expect(small.scale).toBe(1);
    expect(small.x).toBe(0);
    expect(small.y).toBe(0);
  });

  it('GAL-5 pinch keeps the pinch centre in place', () => {
    const next = pinchTo(INITIAL_ZOOM, 1, 100, 200, { x: 80, y: 0 }, box);
    expect(next.scale).toBe(2);
    expect(next.x).toBe(-80);
  });

  it('GAL-5 panning is ignored at 1x and clamped to the picture edges when zoomed', () => {
    expect(panBy(INITIAL_ZOOM, 50, 50, box)).toEqual(INITIAL_ZOOM);
    const zoomed = { scale: 2, x: 0, y: 0 };
    const moved = panBy(zoomed, 5000, -5000, box);
    expect(moved.x).toBe(200);
    expect(moved.y).toBe(-250);
  });

  it('GAL-7 the zoom buttons and keys step by the same amount and stay within 1x..4x', () => {
    const up = stepZoom(INITIAL_ZOOM, 1, box);
    expect(up.scale).toBe(1.5);
    expect(stepZoom(up, -1, box)).toEqual(INITIAL_ZOOM);
    let s = INITIAL_ZOOM;
    for (let i = 0; i < 20; i++) s = stepZoom(s, 1, box);
    expect(s.scale).toBe(MAX_SCALE);
    let t = s;
    for (let i = 0; i < 20; i++) t = stepZoom(t, -1, box);
    expect(t).toEqual(INITIAL_ZOOM);
  });

  it('clampPan keeps the picture covering the box', () => {
    expect(clampPan({ scale: 3, x: 9999, y: -9999 }, box)).toEqual({ scale: 3, x: 400, y: -500 });
    expect(clampPan({ scale: 1, x: 40, y: 40 }, box)).toEqual(INITIAL_ZOOM);
  });

  it('GAL-2 the picture in view follows the scroll position and is clamped to the list', () => {
    expect(imageInView(0, 390, 6)).toBe(0);
    expect(imageInView(390, 390, 6)).toBe(1);
    expect(imageInView(190, 390, 6)).toBe(0);
    expect(imageInView(200, 390, 6)).toBe(1);
    expect(imageInView(99999, 390, 6)).toBe(5);
    expect(imageInView(-20, 390, 6)).toBe(0);
    expect(imageInView(100, 0, 6)).toBe(0);
    expect(imageInView(100, 390, 0)).toBe(0);
  });
});
