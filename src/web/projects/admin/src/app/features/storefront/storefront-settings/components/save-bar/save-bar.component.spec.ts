import { TestBed } from '@angular/core/testing';
import { buttonNamed, textContent } from '@app/testing/dom-helpers';
import { SaveBarComponent } from './save-bar.component';

describe('SaveBarComponent (spec ST-14, ST-15)', () => {
  function create(inputs: { saving?: boolean; dirty?: boolean }) {
    const fixture = TestBed.createComponent(SaveBarComponent);
    fixture.componentRef.setInput('label', 'Save ages');
    for (const [key, value] of Object.entries(inputs)) {
      fixture.componentRef.setInput(key, value);
    }

    const events: string[] = [];
    fixture.componentInstance.save.subscribe(() => events.push('save'));
    fixture.componentInstance.discard.subscribe(() => events.push('discard'));
    fixture.detectChanges();
    return { root: fixture.nativeElement as HTMLElement, events };
  }

  it('is off until something changed', () => {
    const { root } = create({ dirty: false });

    expect(buttonNamed(root, 'Save ages').disabled).toBe(true);
    expect(buttonNamed(root, 'Discard changes').disabled).toBe(true);
    expect(textContent(root)).not.toContain('not saved');
  });

  it('saves or discards when there are changes, and says so in words', () => {
    const { root, events } = create({ dirty: true });

    expect(textContent(root)).toContain('You have changes that are not saved.');
    buttonNamed(root, 'Save ages').click();
    buttonNamed(root, 'Discard changes').click();
    expect(events).toEqual(['save', 'discard']);
  });

  it('cannot be pressed twice while a save is running', () => {
    const { root } = create({ dirty: true, saving: true });

    expect(buttonNamed(root, 'Saving…').disabled).toBe(true);
  });
});
