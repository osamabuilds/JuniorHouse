import { block } from '@app/testing/storefront-fixtures';
import { blankBlock, changeBlockType, cleanBlocks, listItems, tableGrid, withGrid, withListItems } from './blocks.util';

describe('page block helpers (spec ST-8, ST-10, ST-17)', () => {
  it('starts each block type from a valid shape', () => {
    expect(blankBlock('Heading')).toMatchObject({ level: 2, text: '' });
    expect(blankBlock('Fact').factKey).toBe('contact.whatsapp');
    expect(blankBlock('List').items).toHaveLength(1);
    expect(tableGrid(blankBlock('Table'))).toEqual([['', ''], ['', '']]);
    expect(blankBlock('Paragraph')).toMatchObject({ level: 0, factKey: null, items: [] });
  });

  it('keeps what was typed when the type changes between text types, otherwise starts fresh', () => {
    expect(changeBlockType(block({ type: 'Paragraph', text: 'Hello' }), 'Callout')).toMatchObject({ type: 'Callout', text: 'Hello' });
    expect(changeBlockType(block({ type: 'Paragraph', text: 'Hello' }), 'Heading')).toMatchObject({ type: 'Heading', text: 'Hello', level: 2 });
    expect(changeBlockType(block({ type: 'Paragraph', text: 'Hello' }), 'List').text).toBe('');
  });

  it('reads and writes list items in order', () => {
    const list = withListItems(blankBlock('List'), ['one', 'two', 'three']);

    expect(listItems(list)).toEqual(['one', 'two', 'three']);
    expect(list.items.map((i) => [i.row, i.column])).toEqual([[1, 0], [2, 0], [3, 0]]);
  });

  it('reads and writes a table as rows of cells with the header first', () => {
    const table = withGrid(blankBlock('Table'), [['Size', 'Chest'], ['2Y', '52'], ['3Y', '54']]);

    expect(tableGrid(table)).toEqual([['Size', 'Chest'], ['2Y', '52'], ['3Y', '54']]);
    expect(table.items[0]).toEqual({ row: 0, column: 0, text: 'Size' });
  });

  it('drops empty text blocks and empty list lines before saving, but never a fact or a table', () => {
    const cleaned = cleanBlocks([
      block({ type: 'Paragraph', text: '  ' }),
      block({ type: 'Paragraph', text: 'Kept' }),
      withListItems(blankBlock('List'), ['a', '', 'b']),
      withListItems(blankBlock('List'), ['', '']),
      blankBlock('Fact'),
      blankBlock('Table'),
    ]);

    expect(cleaned.map((b) => b.type)).toEqual(['Paragraph', 'List', 'Fact', 'Table']);
    expect(listItems(cleaned[1])).toEqual(['a', 'b']);
  });
});
