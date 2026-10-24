import { BlockType, PageBlock, PageBlockItem } from '../models';

/** A new block of the given type with a sensible shape, so the editor never starts from an invalid block. */
export function blankBlock(type: BlockType): PageBlock {
  const base: PageBlock = { type, text: '', level: 0, factKey: null, label: null, ordered: false, items: [] };
  switch (type) {
    case 'Heading':
      return { ...base, level: 2 };
    case 'List':
      return { ...base, items: [{ row: 1, column: 0, text: '' }] };
    case 'Table':
      return withGrid(base, [['', ''], ['', '']]);
    case 'Fact':
      return { ...base, factKey: 'contact.whatsapp' };
    default:
      return base;
  }
}

/** Changing the type keeps what was typed when the new type is also text, and starts fresh otherwise. */
export function changeBlockType(block: PageBlock, type: BlockType): PageBlock {
  const textual: BlockType[] = ['Paragraph', 'Callout', 'Heading'];
  const fresh = blankBlock(type);
  return textual.includes(block.type) && textual.includes(type) ? { ...fresh, text: block.text } : fresh;
}

export function listItems(block: PageBlock): string[] {
  return [...block.items].sort((a, b) => a.row - b.row).map((i) => i.text);
}

export function withListItems(block: PageBlock, texts: readonly string[]): PageBlock {
  return { ...block, items: texts.map((text, i): PageBlockItem => ({ row: i + 1, column: 0, text })) };
}

/** A table block as rows of cells: row 0 is the header, the rest are the body. */
export function tableGrid(block: PageBlock): string[][] {
  const rows = new Map<number, Map<number, string>>();
  for (const item of block.items) {
    rows.set(item.row, (rows.get(item.row) ?? new Map<number, string>()).set(item.column, item.text));
  }

  const width = Math.max(1, ...block.items.map((i) => i.column + 1));
  return [...rows.keys()].sort((a, b) => a - b).map((row) => Array.from({ length: width }, (_, column) => rows.get(row)?.get(column) ?? ''));
}

export function withGrid(block: PageBlock, grid: readonly (readonly string[])[]): PageBlock {
  return { ...block, items: grid.flatMap((cells, row) => cells.map((text, column): PageBlockItem => ({ row, column, text }))) };
}

/** Empty blocks of text carry nothing, so they are dropped before saving rather than saved as blank paragraphs. */
export function cleanBlocks(blocks: readonly PageBlock[]): PageBlock[] {
  return blocks.filter((b) => {
    switch (b.type) {
      case 'Heading':
      case 'Paragraph':
      case 'Callout':
        return b.text.trim().length > 0;
      case 'List':
        return b.items.some((i) => i.text.trim().length > 0);
      default:
        return true;
    }
  }).map((b) => (b.type === 'List' ? withListItems(b, listItems(b).filter((t) => t.trim().length > 0)) : b));
}
