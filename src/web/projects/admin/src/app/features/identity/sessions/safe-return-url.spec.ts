import { safeReturnUrl } from './safe-return-url';

describe('safeReturnUrl (sessions AC-4)', () => {
  it('keeps a path inside the app, with its query', () => {
    expect(safeReturnUrl('/styles')).toBe('/styles');
    expect(safeReturnUrl('/styles?tab=2&x=%2F')).toBe('/styles?tab=2&x=%2F');
  });

  it.each([null, undefined, '', 'styles', 'https://evil.example', '//evil.example', '/\\evil.example', 'javascript:alert(1)', '/ok\nX-Header: 1', 'http://localhost/x'])(
    'falls back to the home page for %s',
    (value) => {
      expect(safeReturnUrl(value)).toBe('/');
    },
  );
});
