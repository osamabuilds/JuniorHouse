/** A first check of a Pakistani mobile number (03xx xxxxxxx, +92 3xx xxxxxxx). The API is the one that normalises and decides. */
export function looksLikePakistaniMobile(value: string): boolean {
  return /^(?:\+92|0092|92|0)?3\d{9}$/.test(value.replace(/[\s-]/g, ''));
}
