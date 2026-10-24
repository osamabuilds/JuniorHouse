import { TestBed } from '@angular/core/testing';
import { block } from '@app/testing/storefront-fixtures';
import { choose, controlsWithoutLabels, typeInto } from '@app/testing/dom-helpers';
import { PageBlock } from '../../models';
import { HeadingBlockEditorComponent } from './heading-block-editor.component';

describe('HeadingBlockEditorComponent (spec ST-8, ST-17)', () => {
  function create(source: PageBlock = block({ type: 'Heading', text: 'Delivery', level: 2 })) {
    const fixture = TestBed.createComponent(HeadingBlockEditorComponent);
    fixture.componentRef.setInput('block', source);
    fixture.componentRef.setInput('idPrefix', 'h');
    const emitted: PageBlock[] = [];
    fixture.componentInstance.blockChange.subscribe((b) => emitted.push(b));
    fixture.detectChanges();
    return { root: fixture.nativeElement as HTMLElement, emitted };
  }

  it('labels both controls', () => {
    expect(controlsWithoutLabels(create().root)).toEqual([]);
  });

  it('offers a section heading and a sub-heading and shows which one this is', () => {
    const { root } = create();

    expect((root.querySelector('#h-level') as HTMLSelectElement).value).toBe('2');
    expect(Array.from(root.querySelectorAll('#h-level option')).map((o) => (o as HTMLOptionElement).value)).toEqual(['2', '3']);
  });

  it('reports the text typed and the size chosen, keeping the rest of the block', () => {
    const { root, emitted } = create();

    typeInto(root, '#h-text', 'Tracking');
    choose(root, '#h-level', '3');

    expect(emitted[0]).toMatchObject({ type: 'Heading', text: 'Tracking', level: 2 });
    expect(emitted[1]).toMatchObject({ text: 'Delivery', level: 3 });
  });
});
