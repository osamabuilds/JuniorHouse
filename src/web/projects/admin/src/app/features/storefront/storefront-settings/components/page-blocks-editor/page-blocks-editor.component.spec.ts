import { TestBed } from '@angular/core/testing';
import { block } from '@app/testing/storefront-fixtures';
import { buttonNamed, choose, controlsWithoutLabels, textContent, typeInto } from '@app/testing/dom-helpers';
import { PageBlock } from '../../models';
import { PageBlocksEditorComponent } from './page-blocks-editor.component';

describe('PageBlocksEditorComponent (spec ST-8, ST-10, ST-15)', () => {
  const blocks: PageBlock[] = [
    block({ type: 'Heading', text: 'Delivery', level: 2 }),
    block({ type: 'Paragraph', text: 'We pack with care.' }),
    block({ type: 'Fact', text: '', factKey: 'delivery.rules', label: 'Rates' }),
  ];

  function create(list: readonly PageBlock[] = blocks) {
    const fixture = TestBed.createComponent(PageBlocksEditorComponent);
    fixture.componentRef.setInput('blocks', list);
    const emitted: (readonly PageBlock[])[] = [];
    fixture.componentInstance.blocksChange.subscribe((b) => emitted.push(b));
    fixture.detectChanges();
    return { root: fixture.nativeElement as HTMLElement, emitted };
  }

  it('shows each block with the editor for its type, in an ordered list', () => {
    const { root } = create();

    expect(root.querySelectorAll('ol > li').length).toBe(3);
    expect(root.querySelector('app-heading-block-editor')).not.toBeNull();
    expect(root.querySelector('app-text-block-editor')).not.toBeNull();
    expect(root.querySelector('app-fact-block-editor')).not.toBeNull();
    expect(controlsWithoutLabels(root)).toEqual([]);
  });

  it('edits one block and leaves the others', () => {
    const { root, emitted } = create();

    typeInto(root, '#blk-1-text', 'Packed by hand.');

    expect(emitted[0].map((b) => b.text)).toEqual(['Delivery', 'Packed by hand.', '']);
  });

  it('changes the type of a block, keeping text between text types', () => {
    const { root, emitted } = create();

    choose(root, '#blk-type-1', 'Callout');

    expect(emitted[0][1]).toMatchObject({ type: 'Callout', text: 'We pack with care.' });
  });

  it('reorders and removes with buttons that name the block (WCAG 2.5.7)', () => {
    const { root, emitted } = create();

    buttonNamed(root, 'Move block 2 (Paragraph) up').click();
    buttonNamed(root, 'Remove block 3 (Fact)').click();

    expect(emitted[0].map((b) => b.type)).toEqual(['Paragraph', 'Heading', 'Fact']);
    expect(emitted[1].map((b) => b.type)).toEqual(['Heading', 'Paragraph']);
  });

  it('adds the chosen kind of block at the end', () => {
    const { root, emitted } = create();

    choose(root, '#blk-new-type', 'Table');
    buttonNamed(root, 'Add this block').click();

    expect(emitted[0]).toHaveLength(4);
    expect(emitted[0][3].type).toBe('Table');
  });

  it('stops at 60 blocks and says why', () => {
    const { root } = create(Array.from({ length: 60 }, () => block()));

    expect(buttonNamed(root, 'Add this block').disabled).toBe(true);
    expect(textContent(root)).toContain('at most 60 blocks');
  });

  it('says so when the page is empty', () => {
    expect(textContent(create([]).root)).toContain('The page has no content yet');
  });
});
