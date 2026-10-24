import { TestBed } from '@angular/core/testing';
import { controlsWithoutLabels, buttonNamed, setChecked, typeInto } from '@app/testing/dom-helpers';
import { PageBlock } from '../../models';
import { blankBlock, listItems, withListItems } from '../../utils/blocks.util';
import { ListBlockEditorComponent } from './list-block-editor.component';

describe('ListBlockEditorComponent (spec ST-8)', () => {
  function create(source: PageBlock = withListItems(blankBlock('List'), ['one', 'two'])) {
    const fixture = TestBed.createComponent(ListBlockEditorComponent);
    fixture.componentRef.setInput('block', source);
    fixture.componentRef.setInput('idPrefix', 'l');
    const emitted: PageBlock[] = [];
    fixture.componentInstance.blockChange.subscribe((b) => emitted.push(b));
    fixture.detectChanges();
    return { root: fixture.nativeElement as HTMLElement, emitted };
  }

  it('labels every item and the numbered choice', () => {
    expect(controlsWithoutLabels(create().root)).toEqual([]);
  });

  it('edits an item and keeps the others in order', () => {
    const { root, emitted } = create();

    typeInto(root, '#l-item-1', 'second');

    expect(listItems(emitted[0])).toEqual(['one', 'second']);
  });

  it('adds, moves and removes items with buttons (WCAG 2.5.7)', () => {
    const { root, emitted } = create();

    buttonNamed(root, 'Add an item').click();
    buttonNamed(root, 'Move list item 2 up').click();
    buttonNamed(root, 'Remove list item 1').click();

    expect(listItems(emitted[0])).toEqual(['one', 'two', '']);
    expect(listItems(emitted[1])).toEqual(['two', 'one']);
    expect(listItems(emitted[2])).toEqual(['two']);
  });

  it('turns numbering on for steps', () => {
    const { root, emitted } = create();

    setChecked(root, 'input[type=checkbox]', true);

    expect(emitted[0].ordered).toBe(true);
  });
});
