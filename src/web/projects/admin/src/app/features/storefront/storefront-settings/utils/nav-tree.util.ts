import { NavAction, NavItem } from '../models';

/** Where an entry sits: the index at each level, from the top. `[1, 0]` is the first member of the second entry. */
export type NavPath = readonly number[];

/** Three levels in all (S-1). The server enforces it too (ST-1a); the editor stops short of it with a reason. */
export const MAX_NAV_DEPTH = 3;

const KEY_PATTERN = /^[a-z0-9-]{2,40}$/;

export function slugify(text: string): string {
  return text
    .toLowerCase()
    .normalize('NFKD')
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '')
    .slice(0, 40)
    .replace(/-+$/g, '');
}

export function isValidKey(key: string): boolean {
  return KEY_PATTERN.test(key);
}

/** A key from the label that no other entry uses (keys are unique across the tree and never shown to shoppers). */
export function uniqueKey(items: readonly NavItem[], label: string): string {
  const taken = new Set(flatten(items).map((item) => item.key));
  const base = slugify(label).padEnd(2, 'x') || 'entry';
  let candidate = base;
  for (let n = 2; taken.has(candidate); n++) {
    candidate = `${base.slice(0, 36)}-${n}`;
  }

  return candidate;
}

export function flatten(items: readonly NavItem[]): NavItem[] {
  return items.flatMap((item) => [item, ...flatten(item.children)]);
}

/** Positions follow the order on screen, from 1, so the stored order is always the shown order. */
export function renumber(items: readonly NavItem[]): NavItem[] {
  return items.map((item, index) => ({ ...item, sort: index + 1, children: renumber(item.children) }));
}

/** How many levels the entry and everything under it take (a leaf is 1). */
export function height(item: NavItem): number {
  return 1 + Math.max(0, ...item.children.map(height));
}

export function itemAt(items: readonly NavItem[], path: NavPath): NavItem | undefined {
  let level: readonly NavItem[] = items;
  let found: NavItem | undefined;
  for (const index of path) {
    found = level[index];
    if (!found) {
      return undefined;
    }

    level = found.children;
  }

  return found;
}

/** The keys from the top down, which is how the server names an entry in a field error (`navigation.girls.dresses`). */
export function fieldNameAt(items: readonly NavItem[], path: NavPath): string {
  const keys: string[] = [];
  let level: readonly NavItem[] = items;
  for (const index of path) {
    const item = level[index];
    if (!item) {
      break;
    }

    keys.push(item.key);
    level = item.children;
  }

  return `navigation.${keys.join('.')}`;
}

function mapLevel(items: readonly NavItem[], path: NavPath, change: (siblings: NavItem[], index: number) => NavItem[]): NavItem[] {
  const [head, ...rest] = path;
  if (rest.length === 0) {
    return change([...items], head);
  }

  return items.map((item, index) => (index === head ? { ...item, children: mapLevel(item.children, rest, change) } : item));
}

export function updateAt(items: readonly NavItem[], path: NavPath, patch: Partial<NavItem>): NavItem[] {
  return mapLevel(items, path, (siblings, index) => siblings.map((item, i) => (i === index ? { ...item, ...patch } : item)));
}

export function removeAt(items: readonly NavItem[], path: NavPath): NavItem[] {
  return renumber(mapLevel(items, path, (siblings, index) => siblings.filter((_, i) => i !== index)));
}

export function addRoot(items: readonly NavItem[], item: NavItem): NavItem[] {
  return renumber([...items, item]);
}

export function canAddMember(path: NavPath): boolean {
  return path.length < MAX_NAV_DEPTH;
}

/** Adds the new entry as the last member of the entry at `path`. */
export function addMember(items: readonly NavItem[], path: NavPath, item: NavItem): NavItem[] {
  if (!canAddMember(path)) {
    return [...items];
  }

  return renumber(mapLevel(items, path, (siblings, index) =>
    siblings.map((sibling, i) => (i === index ? { ...sibling, children: [...sibling.children, item] } : sibling))));
}

export function canMoveUp(path: NavPath): boolean {
  return path[path.length - 1] > 0;
}

export function canMoveDown(items: readonly NavItem[], path: NavPath): boolean {
  const siblings = path.length === 1 ? items : (itemAt(items, path.slice(0, -1))?.children ?? []);
  return path[path.length - 1] < siblings.length - 1;
}

function swap(items: readonly NavItem[], path: NavPath, offset: -1 | 1): NavItem[] {
  return renumber(mapLevel(items, path, (siblings, index) => {
    const target = index + offset;
    [siblings[index], siblings[target]] = [siblings[target], siblings[index]];
    return siblings;
  }));
}

export function moveUp(items: readonly NavItem[], path: NavPath): NavItem[] {
  return canMoveUp(path) ? swap(items, path, -1) : [...items];
}

export function moveDown(items: readonly NavItem[], path: NavPath): NavItem[] {
  return canMoveDown(items, path) ? swap(items, path, 1) : [...items];
}

/** Indent makes the entry the last member of the one above it; it needs one above and room for its own members. */
export function canIndent(items: readonly NavItem[], path: NavPath): boolean {
  const item = itemAt(items, path);
  return !!item && path[path.length - 1] > 0 && path.length + height(item) <= MAX_NAV_DEPTH;
}

export function indent(items: readonly NavItem[], path: NavPath): NavItem[] {
  const item = itemAt(items, path);
  if (!item || !canIndent(items, path)) {
    return [...items];
  }

  const above: NavPath = [...path.slice(0, -1), path[path.length - 1] - 1];
  const without = mapLevel(items, path, (siblings, index) => siblings.filter((_, i) => i !== index));
  return addMemberUnchecked(without, above, item);
}

/** Outdent makes the entry a sibling placed right after its parent. */
export function canOutdent(path: NavPath): boolean {
  return path.length > 1;
}

export function outdent(items: readonly NavItem[], path: NavPath): NavItem[] {
  const item = itemAt(items, path);
  if (!item || !canOutdent(path)) {
    return [...items];
  }

  const parentPath = path.slice(0, -1);
  const without = mapLevel(items, path, (siblings, index) => siblings.filter((_, i) => i !== index));
  return renumber(mapLevel(without, parentPath, (siblings, index) => {
    siblings.splice(index + 1, 0, item);
    return siblings;
  }));
}

function addMemberUnchecked(items: readonly NavItem[], path: NavPath, item: NavItem): NavItem[] {
  return renumber(mapLevel(items, path, (siblings, index) =>
    siblings.map((sibling, i) => (i === index ? { ...sibling, children: [...sibling.children, item] } : sibling))));
}

/** A blank entry for the editor to fill in. Its key comes from the label and stays put when the label is renamed. */
export function newEntry(items: readonly NavItem[], label: string): NavItem {
  return {
    key: uniqueKey(items, label),
    label,
    kind: 'Link',
    target: { kind: 'Path', reference: '/' },
    sort: 0,
    enabled: true,
    showAges: false,
    children: [],
  };
}

/** Applies one editor action to the tree and returns the new tree; the editor keeps no other logic. */
export function applyNavAction(items: readonly NavItem[], action: NavAction): NavItem[] {
  switch (action.type) {
    case 'update':
      return updateAt(items, action.path, action.patch);
    case 'add-member':
      return addMember(items, action.path, newEntry(items, 'New entry'));
    case 'remove':
      return removeAt(items, action.path);
    case 'move':
      switch (action.direction) {
        case 'up':
          return moveUp(items, action.path);
        case 'down':
          return moveDown(items, action.path);
        case 'indent':
          return indent(items, action.path);
        case 'outdent':
          return outdent(items, action.path);
      }
  }
}
