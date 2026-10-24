import { readdirSync, readFileSync } from 'node:fs';
import { join } from 'node:path';

const SRC = join(process.cwd(), 'projects/storefront/src');

function files(dir: string, ext: string): string[] {
  return readdirSync(dir, { withFileTypes: true }).flatMap((e) =>
    e.isDirectory() ? files(join(dir, e.name), ext) : e.name.endsWith(ext) ? [join(dir, e.name)] : [],
  );
}

const RAW_COLOUR = /#[0-9a-fA-F]{3,8}\b|rgba?\(/;
const ALLOWED = ['_tokens.scss', '_placeholders.scss'];
const scss = files(SRC, '.scss').filter((f) => !ALLOWED.some((a) => f.endsWith(a)));

describe('storefront styles', () => {
  it('DSF-1 uses no raw colours outside the token and placeholder files', () => {
    const offenders = scss.filter((f) => RAW_COLOUR.test(readFileSync(f, 'utf8')));
    expect(offenders).toEqual([]);
  });

  it('DSF-1 uses no box-shadow other than tokens and the swatch ring', () => {
    const offenders = scss.flatMap((f) =>
      readFileSync(f, 'utf8')
        .split('\n')
        .filter((l) => /box-shadow:/.test(l) && !/var\(--shadow-float\)|rp-swatch/.test(l))
        .map((l) => `${f}: ${l.trim()}`),
    );
    expect(offenders).toEqual([]);
  });

  it('DSF-2 has no dark-mode rules (one theme only)', () => {
    const offenders = [...scss, ...files(SRC, '.ts')]
      .filter((f) => !f.endsWith('style-rules.spec.ts'))
      .filter((f) => readFileSync(f, 'utf8').includes('prefers-color-scheme'));
    expect(offenders).toEqual([]);
  });

  it('DSF-3 loads fonts locally with no external font request', () => {
    const html = readFileSync(join(SRC, 'index.html'), 'utf8');
    const angular = readFileSync(join(process.cwd(), 'angular.json'), 'utf8');
    expect(html).not.toMatch(/fonts\.(googleapis|gstatic)\.com/);
    expect(angular).toContain('@fontsource/instrument-serif/400.css');
    expect(angular).toContain('@fontsource-variable/dm-sans/index.css');
    const css = readFileSync(
      join(process.cwd(), 'node_modules/@fontsource/instrument-serif/400.css'),
      'utf8',
    );
    expect(css).toContain('font-display: swap');
  });
});
