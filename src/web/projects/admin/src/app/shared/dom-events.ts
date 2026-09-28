/**
 * Reads a typed value off a DOM event. Angular's template expression parser doesn't support
 * TypeScript's `as` cast syntax (NG5002 "Missing closing parentheses") - `$event.target as
 * HTMLInputElement` has to happen in a component method, not inline in the template. These are
 * that one cast, written once, for every screen to reuse.
 */
export function inputValue(event: Event): string {
  return (event.target as HTMLInputElement).value;
}

export function inputChecked(event: Event): boolean {
  return (event.target as HTMLInputElement).checked;
}

export function inputNumber(event: Event): number {
  return +inputValue(event);
}

export function selectValue(event: Event): string {
  return (event.target as HTMLSelectElement).value;
}

/** For an optional-id `<select>` (an empty option represents "no filter"/"no selection"). */
export function selectNumberOrNull(event: Event): number | null {
  const value = selectValue(event);
  return value ? +value : null;
}
