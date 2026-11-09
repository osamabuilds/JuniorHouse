import { readFileSync } from 'node:fs';
import { join } from 'node:path';

/**
 * DSF-4: contrast per surface, checked on the design system's own token file (docs/design-system/tokens.json), the source of truth for the
 * storefront. tokens.spec.ts checks the compiled stylesheet (SC-17); this test also proves the two files agree, so neither can drift.
 */
const FILE = join(process.cwd(), '../../docs/design-system/tokens.json');
const SCSS = join(process.cwd(), 'projects/storefront/src/styles/_tokens.scss');

const doc = JSON.parse(readFileSync(FILE, 'utf8')) as { color: { tokens: { name: string; value: Record<string, string> }[] } };
const tokens = new Map(doc.color.tokens.map((x) => [x.name, x.value['paper'].toLowerCase()]));

const root = /:root\s*\{([\s\S]*?)\n\}/.exec(readFileSync(SCSS, 'utf8'))![1];
const css = new Map<string, string>([...root.matchAll(/--([a-z0-9-]+):\s*(#[0-9a-fA-F]{6})\s*;/g)].map((m) => [m[1], m[2].toLowerCase()]));

function channel(hex: string, i: number): number {
  const s = parseInt(hex.slice(i, i + 2), 16) / 255;
  return s <= 0.03928 ? s / 12.92 : Math.pow((s + 0.055) / 1.055, 2.4);
}
function luminance(hex: string): number {
  return 0.2126 * channel(hex, 1) + 0.7152 * channel(hex, 3) + 0.0722 * channel(hex, 5);
}
/** Contrast ratio; a token with an alpha channel is read without it (those tokens are overlays, not text). */
function ratio(a: string, b: string): number {
  const [hi, lo] = [luminance(a.slice(0, 7)), luminance(b.slice(0, 7))].sort((x, y) => y - x);
  return (hi + 0.05) / (lo + 0.05);
}
const t = (name: string): string => {
  const v = tokens.get(name);
  if (!v) throw new Error(`tokens.json has no colour token "${name}"`);
  return v;
};

const LIGHT = ['surface', 'surface-raised', 'surface-sunken'];

describe('DSF-4 design-system colour tokens (tokens.json)', () => {
  it.each(LIGHT)('body and secondary text on %s meet 4.5:1, control borders 3:1', (surface) => {
    expect(ratio(t('ink'), t(surface))).toBeGreaterThanOrEqual(4.5);
    expect(ratio(t('ink-muted'), t(surface))).toBeGreaterThanOrEqual(4.5);
    expect(ratio(t('accent-deep'), t(surface))).toBeGreaterThanOrEqual(4.5);
    expect(ratio(t('line-strong'), t(surface))).toBeGreaterThanOrEqual(3);
    expect(ratio(t('success-text'), t(surface))).toBeGreaterThanOrEqual(4.5);
    expect(ratio(t('danger-text'), t(surface))).toBeGreaterThanOrEqual(4.5);
    expect(ratio(t('sale'), t(surface))).toBeGreaterThanOrEqual(4.5);
  });

  it('text on the dark surface meets 4.5:1', () => {
    expect(ratio(t('on-dark'), t('surface-dark'))).toBeGreaterThanOrEqual(4.5);
    expect(ratio(t('on-dark-muted'), t('surface-dark'))).toBeGreaterThanOrEqual(4.5);
  });

  it('labels on filled controls meet 4.5:1', () => {
    expect(ratio(t('on-accent'), t('accent'))).toBeGreaterThanOrEqual(4.5);
    expect(ratio(t('on-accent'), t('accent-deep'))).toBeGreaterThanOrEqual(4.5);
    expect(ratio(t('on-action'), t('action'))).toBeGreaterThanOrEqual(4.5);
    expect(ratio('#ffffff', t('sale'))).toBeGreaterThanOrEqual(4.5);
  });

  it('status text meets 4.5:1 on its own tint', () => {
    expect(ratio(t('success-text'), t('success-tint'))).toBeGreaterThanOrEqual(4.5);
    expect(ratio(t('danger-text'), t('danger-tint'))).toBeGreaterThanOrEqual(4.5);
  });

  it('brand accent is never lighter than the deep accent used for text', () => {
    expect(luminance(t('accent'))).toBeLessThanOrEqual(luminance(t('accent-deep')));
  });

  it('every colour the stylesheet also defines has the same value in tokens.json', () => {
    const shared = [...tokens.keys()].filter((name) => css.has(name) && tokens.get(name)!.length === 7);
    expect(shared.length).toBeGreaterThan(15);
    const differing = shared.filter((name) => css.get(name) !== tokens.get(name));
    expect(differing).toEqual([]);
  });
});
