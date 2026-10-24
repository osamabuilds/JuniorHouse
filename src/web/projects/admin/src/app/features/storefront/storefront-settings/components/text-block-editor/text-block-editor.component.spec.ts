import { TestBed } from '@angular/core/testing';
import { block } from '@app/testing/storefront-fixtures';
import { controlsWithoutLabels, textContent, typeInto } from '@app/testing/dom-helpers';
import { PageBlock } from '../../models';
import { TextBlockEditorComponent } from './text-block-editor.component';

describe('TextBlockEditorComponent (spec ST-8, ST-10)', () => {
  function create(source: PageBlock = block({ text: 'Hello' }), label = 'Paragraph') {
    const fixture = TestBed.createComponent(TextBlockEditorComponent);
    fixture.componentRef.setInput('block', source);
    fixture.componentRef.setInput('idPrefix', 't');
    fixture.componentRef.setInput('label', label);
    const emitted: PageBlock[] = [];
    fixture.componentInstance.blockChange.subscribe((b) => emitted.push(b));
    fixture.detectChanges();
    return { root: fixture.nativeElement as HTMLElement, emitted };
  }

  it('has a label for the kind of text (paragraph or note)', () => {
    const { root } = create(block(), 'Highlighted note');

    expect(controlsWithoutLabels(root)).toEqual([]);
    expect(root.querySelector('label')?.textContent).toBe('Highlighted note');
  });

  it('counts down the characters left, and stops at the paragraph limit', () => {
    const { root } = create(block({ text: 'x'.repeat(4990) }));

    expect(textContent(root)).toContain('10 characters left');
    expect(root.querySelector('textarea')?.getAttribute('maxlength')).toBe('5000');
  });

  it('keeps markup as plain text: it is passed on exactly as typed, never interpreted (ST-10)', () => {
    const { root, emitted } = create();

    typeInto(root, '#t-text', '<script>alert(1)</script>');

    expect(emitted[0].text).toBe('<script>alert(1)</script>');
    expect(root.querySelector('script')).toBeNull();
  });
});
