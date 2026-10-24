import { readFileSync } from 'node:fs';
import { join } from 'node:path';

const SRC = join(process.cwd(), 'projects/storefront/src/styles/_tokens.scss');
const root = /:root\s*\{([\s\S]*?)\n\}/.exec(readFileSync(SRC, 'utf8'))![1];
const tokens = new Map<string, string>(
  [...root.matchAll(/--([a-z0-9-]+):\s*(#[0-9a-fA-F]{6})(?:[0-9a-fA-F]{2})?\s*;/g)].map((m) => [m[1], m[2].toLowerCase()]),
);

function rgb(hex: string): [number, number, number] {
  return [1, 3, 5].map((i) => parseInt(hex.slice(i, i + 2), 16)) as [number, number, number];
}
function luminance(hex: string): number {
  const [r, g, b] = rgb(hex).map((c) => {
    const s = c / 255;
    return s <= 0.03928 ? s / 12.92 : Math.pow((s + 0.055) / 1.055, 2.4);
  });
  return 0.2126 * r + 0.7152 * g + 0.0722 * b;
}
function ratio(a: string, b: string): number {
  const [hi, lo] = [luminance(a), luminance(b)].sort((x, y) => y - x);
  return (hi + 0.05) / (lo + 0.05);
}
const t = (name: string): string => {
  const v = tokens.get(name);
  if (!v) throw new Error(`missing token --${name}`);
  return v;
};

const LIGHT = ['surface', 'surface-raised', 'surface-sunken'];
const DARK = ['surface-dark', 'surface-dark-raised', 'surface-dark-sunken'];

describe('storefront colour tokens (black and white)', () => {
  it('SC-17 uses only neutral greys for surfaces, ink, lines, accent and tones', () => {
    const neutral = [
      ...LIGHT, ...DARK, 'on-dark', 'on-dark-muted', 'line-dark', 'line-strong-dark', 'ink-on-light', 'ink',
      'ink-muted', 'line', 'line-strong', 'accent', 'accent-deep', 'on-accent', 'focus', 'on-image', 'skeleton',
      'skeleton-hi', 'tone-oat', 'tone-stone', 'tone-clay', 'tone-sage', 'tone-sand', 'tone-dusk',
    ];
    const coloured = neutral.filter((n) => {
      const c = rgb(t(n));
      return Math.max(...c) - Math.min(...c) > 2;
    });
    expect(coloured).toEqual([]);
  });

  it.each(LIGHT)('SC-17 text on %s meets 4.5:1 and control borders 3:1', (surface) => {
    expect(ratio(t('ink'), t(surface))).toBeGreaterThanOrEqual(4.5);
    expect(ratio(t('ink-muted'), t(surface))).toBeGreaterThanOrEqual(4.5);
    expect(ratio(t('accent-deep'), t(surface))).toBeGreaterThanOrEqual(4.5);
    expect(ratio(t('line-strong'), t(surface))).toBeGreaterThanOrEqual(3);
    expect(ratio(t('success-text'), t(surface))).toBeGreaterThanOrEqual(4.5);
    expect(ratio(t('danger-text'), t(surface))).toBeGreaterThanOrEqual(4.5);
  });

  it.each(DARK)('SC-17 text on %s meets 4.5:1 and control borders 3:1', (surface) => {
    expect(ratio(t('on-dark'), t(surface))).toBeGreaterThanOrEqual(4.5);
    expect(ratio(t('on-dark-muted'), t(surface))).toBeGreaterThanOrEqual(4.5);
    expect(ratio(t('line-strong-dark'), t(surface))).toBeGreaterThanOrEqual(3);
  });

  it('SC-17 primary button text on the accent fill meets 4.5:1 and hover (ink) stays distinct', () => {
    expect(ratio(t('on-accent'), t('accent-deep'))).toBeGreaterThanOrEqual(4.5);
    expect(t('accent-deep')).not.toBe(t('ink'));
  });
});
