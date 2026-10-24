import { TestBed } from '@angular/core/testing';
import { buttonNamed, hasButton } from '@app/testing/dom-helpers';
import { RowActionsComponent } from './row-actions.component';

describe('RowActionsComponent (spec ST-15)', () => {
  function create(inputs: { first?: boolean; last?: boolean; removable?: boolean } = {}) {
    const fixture = TestBed.createComponent(RowActionsComponent);
    fixture.componentRef.setInput('name', 'the 3Y age');
    for (const [key, value] of Object.entries(inputs)) {
      fixture.componentRef.setInput(key, value);
    }

    const events: string[] = [];
    fixture.componentInstance.up.subscribe(() => events.push('up'));
    fixture.componentInstance.down.subscribe(() => events.push('down'));
    fixture.componentInstance.remove.subscribe(() => events.push('remove'));
    fixture.detectChanges();
    return { root: fixture.nativeElement as HTMLElement, events };
  }

  it('names the row in every button, so a screen reader says what each one does (WCAG 2.4.6)', () => {
    const { root } = create();

    expect(buttonNamed(root, 'Move the 3Y age up')).toBeTruthy();
    expect(buttonNamed(root, 'Move the 3Y age down')).toBeTruthy();
    expect(buttonNamed(root, 'Remove the 3Y age')).toBeTruthy();
    expect(root.querySelector('[role="group"]')?.getAttribute('aria-label')).toBe('Actions for the 3Y age');
  });

  it('reorders and removes with plain buttons (WCAG 2.5.7)', () => {
    const { root, events } = create();

    buttonNamed(root, /up$/).click();
    buttonNamed(root, /down$/).click();
    buttonNamed(root, /^Remove/).click();

    expect(events).toEqual(['up', 'down', 'remove']);
  });

  it('switches off the move that cannot happen at the ends of the list', () => {
    expect(buttonNamed(create({ first: true }).root, /up$/).disabled).toBe(true);
    expect(buttonNamed(create({ last: true }).root, /down$/).disabled).toBe(true);
    expect(buttonNamed(create().root, /up$/).disabled).toBe(false);
  });

  it('can leave removal out for a row that must stay', () => {
    expect(hasButton(create({ removable: false }).root, /^Remove/)).toBe(false);
  });
});
