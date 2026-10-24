import { TestBed } from '@angular/core/testing';
import { buttonNamed, controlsWithoutLabels, typeInto } from '@app/testing/dom-helpers';
import { PageBlock } from '../../models';
import { blankBlock, tableGrid, withGrid } from '../../utils/blocks.util';
import { TableBlockEditorComponent } from './table-block-editor.component';

describe('TableBlockEditorComponent (spec ST-8)', () => {
  const source = withGrid(blankBlock('Table'), [['Size', 'Chest'], ['2Y', '52'], ['3Y', '54']]);

  function create(block: PageBlock = source) {
    const fixture = TestBed.createComponent(TableBlockEditorComponent);
    fixture.componentRef.setInput('block', block);
    fixture.componentRef.setInput('idPrefix', 'tb');
    const emitted: PageBlock[] = [];
    fixture.componentInstance.blockChange.subscribe((b) => emitted.push(b));
    fixture.detectChanges();
    return { root: fixture.nativeElement as HTMLElement, emitted };
  }

  it('is a real table with column headers, and every cell has a label', () => {
    const { root } = create();

    expect(root.querySelectorAll('th[scope="col"]').length).toBe(2);
    expect(root.querySelectorAll('tbody tr').length).toBe(2);
    expect(controlsWithoutLabels(root)).toEqual([]);
  });

  it('edits one cell without disturbing the rest', () => {
    const { root, emitted } = create();

    typeInto(root, '#tb-2-1', '55');
    typeInto(root, '#tb-h-0', 'Age');

    expect(tableGrid(emitted[0])).toEqual([['Size', 'Chest'], ['2Y', '52'], ['3Y', '55']]);
    expect(tableGrid(emitted[1])[0]).toEqual(['Age', 'Chest']);
  });

  it('adds and removes rows and columns with buttons', () => {
    const { root, emitted } = create();

    buttonNamed(root, 'Add a row').click();
    buttonNamed(root, 'Remove the last row').click();
    buttonNamed(root, 'Add a column').click();
    buttonNamed(root, 'Remove the last column').click();

    expect(tableGrid(emitted[0])).toHaveLength(4);
    expect(tableGrid(emitted[1])).toHaveLength(2);
    expect(tableGrid(emitted[2])[0]).toEqual(['Size', 'Chest', '']);
    expect(tableGrid(emitted[3])[0]).toEqual(['Size']);
  });

  it('cannot remove the last column or the header row', () => {
    const { root } = create(withGrid(blankBlock('Table'), [['Only']]));

    expect(buttonNamed(root, 'Remove the last row').disabled).toBe(true);
    expect(buttonNamed(root, 'Remove the last column').disabled).toBe(true);
  });

  it('sets the caption, or clears it', () => {
    const { root, emitted } = create();

    typeInto(root, '#tb-caption', 'Sizes');
    typeInto(root, '#tb-caption', '');

    expect(emitted[0].label).toBe('Sizes');
    expect(emitted[1].label).toBeNull();
  });
});
