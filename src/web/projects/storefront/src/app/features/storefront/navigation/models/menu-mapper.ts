import { NavItem } from '../../../../shared/models/site-settings.model';
import { Menu, MenuEntry } from './menu.model';

/** Splits /girls?age=age-2y into a path and its query. Anything unsafe is left for resolveHref to refuse. */
function split(path: string): { path: string; query?: Record<string, string> } {
  const at = path.indexOf('?');
  if (at < 0) return { path };
  const query: Record<string, string> = {};
  for (const [k, v] of new URLSearchParams(path.slice(at + 1))) query[k] = v;
  return { path: path.slice(0, at), query };
}

function link(key: string, label: string, path: string, sort: number): NavItem {
  const { path: p, query } = split(path);
  return { key, label, kind: 'link', target: { kind: 'path', path: p }, enabled: true, sort, children: [], ...(query ? { query } : {}) };
}

function column(key: string, label: string, sort: number, entries: readonly MenuEntry[]): NavItem | null {
  if (entries.length === 0) return null;
  return {
    key,
    label,
    kind: 'group',
    enabled: true,
    sort,
    children: entries.map((e, i) => link(`${key}-${e.kind}-${e.code || 'all'}`, e.label, e.path, i)),
  };
}

/**
 * The menu from the server as the navigation tree the header, mega-menu and mobile menu already render: each audience opens a
 * panel of columns (Shop, Categories, Collections, Shop by age); New in, Collections and Sale follow as shared entries. Pure.
 */
export function toNavItems(menu: Menu | null): readonly NavItem[] {
  if (!menu) return [];
  const items: NavItem[] = [];

  menu.audiences.forEach((a, i) => {
    const key = a.code.toLowerCase();
    const columns = [
      column(`${key}-shop`, 'Shop', 0, a.entries.filter((e) => e.kind === 'new-in' || e.kind === 'sale')),
      column(`${key}-categories`, 'Categories', 1, a.entries.filter((e) => e.kind === 'category')),
      column(`${key}-collections`, 'Collections', 2, a.entries.filter((e) => e.kind === 'collection')),
      column(`${key}-ages`, 'Shop by age', 3, a.entries.filter((e) => e.kind === 'age')),
    ].filter((c): c is NavItem => c !== null);
    items.push({ key, label: a.name, kind: 'group', target: { kind: 'path', path: a.path }, enabled: true, sort: i, children: columns });
  });

  const shared = menu.audiences.length;
  if (menu.hasNewIn) items.push(link('new-in', 'New in', '/new-in', shared));
  const collections = column('collections', 'Collections', shared + 1, menu.collections);
  if (collections) items.push(collections);
  if (menu.hasSale) items.push(link('sale', 'Sale', '/sale', shared + 2));
  return items;
}
