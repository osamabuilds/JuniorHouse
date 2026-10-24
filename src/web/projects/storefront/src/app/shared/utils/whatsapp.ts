/** International digits only (8 to 15). Anything else gives no link, so a typo never produces a broken one. */
export function whatsappLink(digits: string): { href: string; display: string } | null {
  if (!/^\d{8,15}$/.test(digits)) return null;
  return { href: `https://wa.me/${digits}`, display: `+${digits.slice(0, 2)} ${digits.slice(2, 5)} ${digits.slice(5)}` };
}
