import { TestBed } from '@angular/core/testing';
import { block } from '@app/testing/storefront-fixtures';
import { choose, controlsWithoutLabels, textContent, typeInto } from '@app/testing/dom-helpers';
import { PageBlock } from '../../models';
import { FactBlockEditorComponent } from './fact-block-editor.component';

describe('FactBlockEditorComponent (spec ST-7, ST-17)', () => {
  function create(source: PageBlock = block({ type: 'Fact', text: '', factKey: 'delivery.rules', label: 'Where we deliver' })) {
    const fixture = TestBed.createComponent(FactBlockEditorComponent);
    fixture.componentRef.setInput('block', source);
    fixture.componentRef.setInput('idPrefix', 'f');
    const emitted: PageBlock[] = [];
    fixture.componentInstance.blockChange.subscribe((b) => emitted.push(b));
    fixture.detectChanges();
    return { root: fixture.nativeElement as HTMLElement, emitted };
  }

  it('labels both controls', () => {
    expect(controlsWithoutLabels(create().root)).toEqual([]);
  });

  it('offers the six values a page may quote, in plain words, and shows the chosen one', () => {
    const { root } = create();

    const options = Array.from(root.querySelectorAll('#f-fact option')).map((o) => (o as HTMLOptionElement).textContent?.trim());
    expect(options).toEqual(['WhatsApp number', 'Email address', 'Opening hours', 'Exchange window', 'Exchange conditions', 'Delivery table']);
    expect((root.querySelector('#f-fact') as HTMLSelectElement).value).toBe('delivery.rules');
    expect(textContent(root)).toContain('Filled in under Settings or Delivery and exchange');
  });

  it('reports the value chosen, and the heading typed or cleared', () => {
    const { root, emitted } = create();

    choose(root, '#f-fact', 'exchange.window');
    typeInto(root, '#f-label', '');

    expect(emitted[0].factKey).toBe('exchange.window');
    expect(emitted[1].label).toBeNull();
  });
});
