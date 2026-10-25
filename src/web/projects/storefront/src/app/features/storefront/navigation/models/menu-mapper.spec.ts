import { sampleMenu } from '../testing/sample-menu';
import { toNavItems } from './menu-mapper';

describe('toNavItems', () => {
  it('NAV-1 lists the audiences, then New in, Collections and Sale', () => {
    expect(toNavItems(sampleMenu).map((i) => i.label)).toEqual(['Girls', 'Boys', 'New in', 'Collections', 'Sale']);
  });

  it('NAV-1 an audience opens columns: Shop, Categories, Collections and Shop by age, each only when it has links', () => {
    const [girls, boys] = toNavItems(sampleMenu);
    expect(girls.children.map((c) => c.label)).toEqual(['Shop', 'Categories', 'Collections', 'Shop by age']);
    expect(boys.children.map((c) => c.label)).toEqual(['Shop', 'Categories']);
  });

  it('NAV-8 an age link keeps its code in the query, not in the path', () => {
    const age = toNavItems(sampleMenu)[0].children[3].children[0];
    expect(age.target).toEqual({ kind: 'path', path: '/girls' });
    expect(age.query).toEqual({ age: 'age-2y' });
  });

  it('NAV-2 every link is a path target, so resolveHref can vet it', () => {
    const links = toNavItems(sampleMenu).flatMap((i) => [i, ...i.children.flatMap((c) => [c, ...c.children])]);
    for (const l of links.filter((i) => i.kind === 'link')) expect(l.target?.kind).toBe('path');
  });

  it('NAV-3 shared entries are left out when the server says there are none', () => {
    const items = toNavItems({ audiences: [], collections: [], hasNewIn: false, hasSale: false });
    expect(items).toEqual([]);
  });

  it('NAV-3 no menu yet gives no entries', () => {
    expect(toNavItems(null)).toEqual([]);
  });

  it('NAV-5 a new audience, category or collection from the server appears with no code change', () => {
    const items = toNavItems({
      ...sampleMenu,
      audiences: [
        ...sampleMenu.audiences,
        { code: 'BABY', name: 'Baby', path: '/baby', entries: [{ kind: 'category', code: 'ROMPERS', label: 'Rompers', path: '/baby/rompers' }] },
      ],
    });
    const baby = items.find((i) => i.label === 'Baby')!;
    expect(baby.children[0].children[0].target).toEqual({ kind: 'path', path: '/baby/rompers' });
  });
});
