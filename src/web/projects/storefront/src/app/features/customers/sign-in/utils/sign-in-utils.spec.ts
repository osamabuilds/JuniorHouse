import { looksLikePakistaniMobile } from './mobile.util';
import { checkPassword, isPasswordValid } from './password-rules.util';
import { safeReturnUrl } from './return-url.util';

describe('password rules', () => {
  it('ACT-2 lists every rule in a fixed order and marks each as met or not', () => {
    const rules = checkPassword('abc');
    expect(rules.map((r) => r.code)).toEqual(['LENGTH', 'CAPITAL', 'SMALL', 'DIGIT', 'NO_SPACES']);
    expect(rules.map((r) => r.met)).toEqual([false, false, true, false, true]);
  });

  it('ACT-2 accepts a password with a capital, a small letter, a digit and special characters', () => {
    expect(isPasswordValid('Sunny#Day9!')).toBe(true);
  });

  it('ACT-2 refuses spaces, short and over-long passwords', () => {
    expect(isPasswordValid('Sunny Day 99')).toBe(false);
    expect(isPasswordValid('Aa1')).toBe(false);
    expect(isPasswordValid('Aa1' + 'x'.repeat(126))).toBe(false);
    expect(isPasswordValid('Aa1' + 'x'.repeat(125))).toBe(true);
  });
});

describe('safeReturnUrl', () => {
  it('ACT-4 keeps a path on this site', () => {
    expect(safeReturnUrl('/account/addresses')).toBe('/account/addresses');
  });

  it.each(['https://evil.example', '//evil.example', '/\\evil.example', 'javascript:alert(1)', '', null, undefined, '/a\nb'])('never leaves the site: %s', (value) => {
    expect(safeReturnUrl(value as string | null | undefined)).toBe('/account');
  });
});

describe('looksLikePakistaniMobile', () => {
  it.each(['0300 1234567', '+92 300 1234567', '03001234567', '0300-1234567'])('accepts %s', (v) => expect(looksLikePakistaniMobile(v)).toBe(true));
  it.each(['', '12345', '0200 1234567', '+44 7700 900123'])('refuses %s', (v) => expect(looksLikePakistaniMobile(v)).toBe(false));
});
