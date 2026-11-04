const BACKSLASH = String.fromCharCode(92);

/** True when the text holds a control character (code 0 to 31). */
const hasControlCharacter = (value: string): boolean => [...value].some((c) => c.charCodeAt(0) < 32);

/** Where to go after signing in: only a path on this site (never another site, never a protocol-relative address), else the account. */
export function safeReturnUrl(value: string | null | undefined): string {
  if (!value || !value.startsWith('/') || value.startsWith('//') || value.includes(BACKSLASH) || hasControlCharacter(value)) return '/account';
  return value;
}
