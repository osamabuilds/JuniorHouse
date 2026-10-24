/** Pure helpers for the ordered lists the settings screens edit (ages, footer columns and links, searches, conditions, blocks). */

export function moveItem<T>(list: readonly T[], index: number, delta: -1 | 1): T[] {
  const target = index + delta;
  if (index < 0 || index >= list.length || target < 0 || target >= list.length) {
    return [...list];
  }

  const copy = [...list];
  [copy[index], copy[target]] = [copy[target], copy[index]];
  return copy;
}

export function removeItem<T>(list: readonly T[], index: number): T[] {
  return list.filter((_, i) => i !== index);
}

export function replaceItem<T>(list: readonly T[], index: number, patch: Partial<T>): T[] {
  return list.map((item, i) => (i === index ? { ...item, ...patch } : item));
}

/** Gives each item a `sort` that follows its place on screen, from 1, so the stored order is the shown order. */
export function withSort<T extends { sort: number }>(list: readonly T[]): T[] {
  return list.map((item, i) => ({ ...item, sort: i + 1 }));
}

export function canMove(list: readonly unknown[], index: number, delta: -1 | 1): boolean {
  const target = index + delta;
  return target >= 0 && target < list.length;
}
