/**
 * Where to go after signing in. Only a path inside this app is honoured: anything that could leave the site (another host, a
 * protocol-relative "//host", a backslash trick, a script URL) falls back to the home page, so a crafted sign-in link cannot send
 * a freshly signed-in person somewhere else.
 */
export function safeReturnUrl(value: string | null | undefined): string {
  // eslint-disable-next-line no-control-regex
  if (!value || !value.startsWith('/') || value.startsWith('//') || value.includes('\\') || /[\u0000-\u001f]/.test(value)) {
    return '/';
  }

  return value;
}
