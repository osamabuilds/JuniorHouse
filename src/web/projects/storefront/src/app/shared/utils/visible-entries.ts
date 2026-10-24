import { NavItem } from '../models/site-settings.model';

/** Enabled items only, in order, recursively. A group left with no visible member disappears. */
export function visibleEntries(items: readonly NavItem[]): readonly NavItem[] {
  return [...items]
    .filter((i) => i.enabled)
    .sort((a, b) => a.sort - b.sort)
    .map((i) => ({ ...i, children: visibleEntries(i.children) }))
    .filter((i) => i.kind !== 'group' || i.children.length > 0);
}
