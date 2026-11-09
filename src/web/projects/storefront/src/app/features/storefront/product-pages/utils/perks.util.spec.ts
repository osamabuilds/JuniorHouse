import { describe, expect, it } from 'vitest';
import { FactsView } from '../../site-settings';
import { buildPerks } from './perks.util';

const rule = (over: Partial<FactsView['deliveryRules'][number]> = {}) => ({ city: 'Lahore', minDays: 2, maxDays: 4, charge: 200, courier: 'TCS', enabled: true, ...over });
const facts = (over: Partial<FactsView> = {}): FactsView => ({
  whatsapp: { href: 'https://wa.me/923315613217', display: '+92 331 5613217' },
  email: null,
  hours: null,
  exchangeWindowDays: 7,
  exchangeConditions: [],
  deliveryRules: [rule()],
  ...over,
});

describe('buildPerks', () => {
  it('BUY-10 builds delivery, exchange and WhatsApp lines from the settings, in that order', () => {
    const perks = buildPerks(facts());
    expect(perks.map((p) => p.icon)).toEqual(['truck', 'swap', 'chat']);
    expect(perks[0].text).toBe('Delivery in 2-4 days');
    expect(perks[1].text).toBe('Exchange within 7 days');
    expect(perks[2].text).toBe('Questions? Message us on WhatsApp');
    expect(perks[2].href).toBe('https://wa.me/923315613217');
  });

  it('BUY-10 the delivery range spans every enabled rule and ignores disabled ones', () => {
    const perks = buildPerks(facts({ deliveryRules: [rule({ minDays: 3, maxDays: 5 }), rule({ minDays: 1, maxDays: 2 }), rule({ minDays: 9, maxDays: 12, enabled: false })] }));
    expect(perks[0].text).toBe('Delivery in 1-5 days');
  });

  it('BUY-10 one fixed number of days reads as a single figure, and one day is singular', () => {
    expect(buildPerks(facts({ deliveryRules: [rule({ minDays: 3, maxDays: 3 })] }))[0].text).toBe('Delivery in 3 days');
    expect(buildPerks(facts({ deliveryRules: [rule({ minDays: 1, maxDays: 1 })] }))[0].text).toBe('Delivery in 1 day');
  });

  it('BUY-11 a missing fact means a missing perk, never an empty one', () => {
    expect(buildPerks(facts({ deliveryRules: [] })).map((p) => p.icon)).toEqual(['swap', 'chat']);
    expect(buildPerks(facts({ deliveryRules: [rule({ enabled: false })] })).map((p) => p.icon)).toEqual(['swap', 'chat']);
    expect(buildPerks(facts({ deliveryRules: [rule({ minDays: 0, maxDays: 0 })] })).map((p) => p.icon)).toEqual(['swap', 'chat']);
    expect(buildPerks(facts({ exchangeWindowDays: null })).map((p) => p.icon)).toEqual(['truck', 'chat']);
    expect(buildPerks(facts({ whatsapp: null })).map((p) => p.icon)).toEqual(['truck', 'swap']);
    expect(buildPerks(facts({ whatsapp: null, exchangeWindowDays: null, deliveryRules: [] }))).toEqual([]);
  });

  it('BUY-10 one day for an exchange window is singular too', () => {
    expect(buildPerks(facts({ exchangeWindowDays: 1 }))[1].text).toBe('Exchange within 1 day');
  });
});
