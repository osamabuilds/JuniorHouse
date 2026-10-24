import { NavItem, SiteSettings } from '../models/site-settings.model';
import { launchSettings } from '../data/launch-settings';
import { validateSettings } from './validate-settings';
import { visibleEntries } from './visible-entries';

const item = (key: string, over: Partial<NavItem> = {}): NavItem => ({
  key, label: key, kind: 'link', target: { kind: 'path', path: `/${key}` }, enabled: true, sort: 1, children: [], ...over,
});
const withNav = (navigation: NavItem[]): SiteSettings => ({ ...launchSettings, navigation });

describe('validateSettings', () => {
  it('SC-3 SC-12 SC-14 the launch set is clean', () => {
    expect(validateSettings(launchSettings)).toEqual([]);
  });

  it('SC-4 rejects duplicate navigation keys', () => {
    const problems = validateSettings(withNav([item('girls'), item('girls', { sort: 2 })]));
    expect(problems.some((p) => p.includes('duplicate navigation key "girls"'))).toBe(true);
  });

  it('SC-4 rejects a fourth level', () => {
    const l4 = item('l4');
    const l3 = item('l3', { children: [l4] });
    const l2 = item('l2', { children: [l3] });
    const problems = validateSettings(withNav([item('l1', { children: [l2] })]));
    expect(problems.some((p) => p.includes('deeper than 3 levels'))).toBe(true);
  });

  it('SC-4 rejects a group with no enabled member', () => {
    const group = item('boys', { kind: 'group', target: undefined, children: [item('x', { enabled: false })] });
    expect(validateSettings(withNav([group])).some((p) => p.includes('"boys" has no enabled members'))).toBe(true);
  });

  it('SC-4 rejects links to unknown pages or routes and unsafe targets', () => {
    const bad = [
      item('a', { target: { kind: 'page', slug: 'nope' } }),
      item('b', { target: { kind: 'path', path: '/unknown-area/x' } }),
      item('c', { target: { kind: 'external', url: 'javascript:alert(1)' } }),
    ];
    const problems = validateSettings(withNav(bad));
    expect(problems.filter((p) => p.includes('dead or unsafe link')).length).toBe(3);
  });

  it('SC-4 rejects an age without a code or with a duplicate code', () => {
    const ages = [{ code: '', label: '1Y', sort: 1, enabled: true }, { code: '2y', label: '2Y', sort: 2, enabled: true }, { code: '2y', label: '2Y again', sort: 3, enabled: true }];
    const problems = validateSettings({ ...launchSettings, ages });
    expect(problems.some((p) => p.includes('age without a code'))).toBe(true);
    expect(problems.some((p) => p.includes('duplicate age code "2y"'))).toBe(true);
  });
});

describe('visibleEntries', () => {
  it('SC-1 keeps enabled items only, sorted, recursively', () => {
    const nav = [
      item('b', { sort: 2 }),
      item('a', { sort: 1, kind: 'group', target: undefined, children: [item('m2', { sort: 2 }), item('m1', { sort: 1 }), item('off', { enabled: false })] }),
      item('hidden', { enabled: false }),
    ];
    const out = visibleEntries(nav);
    expect(out.map((i) => i.key)).toEqual(['a', 'b']);
    expect(out[0].children.map((i) => i.key)).toEqual(['m1', 'm2']);
  });

  it('SC-3a drops a group whose members are all disabled', () => {
    const nav = [item('g', { kind: 'group', target: undefined, children: [item('x', { enabled: false })] })];
    expect(visibleEntries(nav)).toEqual([]);
  });

  it('SC-1 the launch set shows Girls, Boys, New in, Collections and Sale in that order', () => {
    expect(visibleEntries(launchSettings.navigation).map((i) => i.label)).toEqual(['Girls', 'Boys', 'New in', 'Collections', 'Sale']);
  });

  it('SC-3a the launch set nests members and further members under Boys and Girls', () => {
    const boys = visibleEntries(launchSettings.navigation).find((i) => i.key === 'boys')!;
    expect(boys.children.map((c) => c.label)).toEqual(expect.arrayContaining(['Tees and shirts', 'Bottoms', 'Casual shirts']));
    expect(boys.children.some((c) => c.children.length > 0)).toBe(true);
  });

  it('SC-5 the launch set ages run 1Y to 8Y one year apart', () => {
    expect(launchSettings.ages.map((a) => a.label)).toEqual(['1Y', '2Y', '3Y', '4Y', '5Y', '6Y', '7Y', '8Y']);
  });

  it('O-7 the WhatsApp number is held in the brand settings', () => {
    expect(launchSettings.brand.whatsapp).toBe('923315613217');
  });
});
