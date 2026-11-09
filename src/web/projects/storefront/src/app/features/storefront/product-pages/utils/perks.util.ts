import { FactsView } from '../../site-settings';

export interface Perk {
  readonly icon: 'truck' | 'swap' | 'chat';
  readonly text: string;
  /** Only the WhatsApp line is a link. */
  readonly href?: string;
}

const days = (n: number): string => `${n} ${n === 1 ? 'day' : 'days'}`;

/**
 * The three short promises under the buy button (design system: ProductDetail; BUY-10, BUY-11). Every figure comes from the settings the
 * business maintains; a fact that is missing leaves its line out, so the page never shows an empty or invented promise. Pure.
 */
export function buildPerks(facts: FactsView): readonly Perk[] {
  const perks: Perk[] = [];

  const rules = facts.deliveryRules.filter((r) => r.enabled && r.minDays > 0 && r.maxDays >= r.minDays);
  if (rules.length > 0) {
    const min = Math.min(...rules.map((r) => r.minDays));
    const max = Math.max(...rules.map((r) => r.maxDays));
    perks.push({ icon: 'truck', text: `Delivery in ${min === max ? days(min) : `${min}-${days(max)}`}` });
  }

  if (facts.exchangeWindowDays !== null && facts.exchangeWindowDays > 0) {
    perks.push({ icon: 'swap', text: `Exchange within ${days(facts.exchangeWindowDays)}` });
  }

  if (facts.whatsapp) {
    perks.push({ icon: 'chat', text: 'Questions? Message us on WhatsApp', href: facts.whatsapp.href });
  }

  return perks;
}
