/** Small DOM helpers for component specs: find a control, type into it, press a button by its visible or accessible name. */

export const textContent = (root: HTMLElement): string => (root.textContent ?? '').replace(/\s+/g, ' ');

export function typeInto(root: ParentNode, selector: string, value: string): void {
  const element = root.querySelector(selector) as HTMLInputElement | HTMLTextAreaElement | null;
  if (!element) {
    throw new Error(`No control matches ${selector}`);
  }

  element.value = value;
  element.dispatchEvent(new Event('input'));
}

export function choose(root: ParentNode, selector: string, value: string): void {
  const element = root.querySelector(selector) as HTMLSelectElement | null;
  if (!element) {
    throw new Error(`No select matches ${selector}`);
  }

  element.value = value;
  element.dispatchEvent(new Event('change'));
}

export function setChecked(root: ParentNode, selector: string, checked: boolean): void {
  const element = root.querySelector(selector) as HTMLInputElement | null;
  if (!element) {
    throw new Error(`No checkbox matches ${selector}`);
  }

  element.checked = checked;
  element.dispatchEvent(new Event('change'));
}

/** The button whose accessible name (aria-label, else its text) matches. */
export function buttonNamed(root: ParentNode, name: RegExp | string): HTMLButtonElement {
  const matches = (b: HTMLButtonElement) => {
    const label = b.getAttribute('aria-label') ?? b.textContent ?? '';
    return typeof name === 'string' ? label.trim() === name : name.test(label);
  };
  const button = Array.from(root.querySelectorAll('button')).find(matches);
  if (!button) {
    throw new Error(`No button named ${String(name)}`);
  }

  return button;
}

export const hasButton = (root: ParentNode, name: RegExp | string): boolean => {
  try {
    buttonNamed(root, name);
    return true;
  } catch {
    return false;
  }
};

/** Every visible control that takes input has a label that points at it (WCAG 1.3.1, 3.3.2). */
export function controlsWithoutLabels(root: HTMLElement): string[] {
  return Array.from(root.querySelectorAll('input, select, textarea'))
    .filter((control) => !control.closest('label') && !control.getAttribute('aria-label'))
    .map((control) => control.getAttribute('id') ?? '')
    .filter((id) => id === '' || !root.querySelector(`label[for="${id}"]`));
}
