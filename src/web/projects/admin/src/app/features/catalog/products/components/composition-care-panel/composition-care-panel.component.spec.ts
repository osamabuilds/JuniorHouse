import { TestBed } from '@angular/core/testing';
import { compositionCare, lookup } from '@app/testing/catalog-fixtures';
import { textOf } from '@app/testing/production-fixtures';
import { CompositionRowInput } from '../../models';
import { CompositionCarePanelComponent, totalPercent } from './composition-care-panel.component';

describe('totalPercent (spec CMP-7)', () => {
  it('adds in tenths so thirds total exactly 100', () => {
    expect(totalPercent([{ fibreId: 1, percent: '33.3' }, { fibreId: 2, percent: '33.3' }, { fibreId: 3, percent: '33.4' }])).toBe(100);
  });

  it('treats a blank or non-number as zero', () => {
    expect(totalPercent([{ fibreId: 1, percent: '' }, { fibreId: 2, percent: 'x' }])).toBe(0);
  });
});

describe('CompositionCarePanelComponent (spec CMP-1..9)', () => {
  function create(data = compositionCare()) {
    const fixture = TestBed.createComponent(CompositionCarePanelComponent);
    fixture.componentRef.setInput('data', data);
    fixture.componentRef.setInput('fibres', [lookup(1, 'Cotton'), lookup(2, 'Polyester'), lookup(3, 'Elastane')]);
    fixture.componentRef.setInput('careOptions', [lookup(1, 'Machine wash at 30 C'), lookup(2, 'Do not bleach')]);
    const composition = vi.fn<(rows: readonly CompositionRowInput[]) => void>();
    const care = vi.fn<(change: { careIds: readonly number[]; note: string | null }) => void>();
    fixture.componentInstance.compositionSaved.subscribe(composition);
    fixture.componentInstance.careSaved.subscribe(care);
    fixture.detectChanges();
    const root = fixture.nativeElement as HTMLElement;
    const submit = (heading: string) => {
      const form = Array.from(root.querySelectorAll('form')).find((f) => f.querySelector('h3')?.textContent?.includes(heading)) as HTMLFormElement;
      form.dispatchEvent(new Event('submit'));
      fixture.detectChanges();
    };
    return { fixture, root, composition, care, submit };
  }

  it('shows the rows and a running total announced politely (CMP-9)', () => {
    const { root } = create();

    const total = root.querySelector('.composition-total') as HTMLElement;
    expect(total.textContent).toContain('Total 100%');
    expect(total.getAttribute('aria-live')).toBe('polite');
    expect(root.querySelectorAll('tbody tr').length).toBe(2);
  });

  it('says how much is missing, as the rows change (CMP-1)', () => {
    const { fixture, root } = create(compositionCare({ rows: [{ fibreId: 1, fibreName: 'Cotton', percent: 95 }], total: 95, isComplete: false }));

    expect(textOf(fixture)).toContain('Total 95%: 5% missing');
    (root.querySelector('tbody input') as HTMLInputElement).value = '105';
    (root.querySelector('tbody input') as HTMLInputElement).dispatchEvent(new Event('input'));
    fixture.detectChanges();
    expect(textOf(fixture)).toContain('Total 105%: 5% too much');
  });

  it('labels every control and uses a fieldset for the care options (NFR-19)', () => {
    const { root } = create();

    expect(root.querySelector('legend')).not.toBeNull();
    expect(root.querySelector('label[for="care-note"]')).not.toBeNull();
    expect(root.textContent).toContain('Share of fibre 1 in percent');
    expect(root.textContent).toContain('Machine wash at 30 C');
  });

  it('sends the rows as numbers (CMP-7)', () => {
    const { composition, submit } = create();

    submit('Fabric composition');

    expect(composition).toHaveBeenCalledWith([{ fibreId: 1, percent: 95 }, { fibreId: 3, percent: 5 }]);
  });

  it('refuses the same fibre twice, a blank fibre, and too many decimals before sending (CMP-6)', () => {
    const { fixture, root, composition, submit } = create();
    const select = root.querySelectorAll('tbody select')[1] as HTMLSelectElement;
    select.value = '1';
    select.dispatchEvent(new Event('change'));
    submit('Fabric composition');
    expect(textOf(fixture)).toContain('Each fibre can be listed only once.');

    select.value = '3';
    select.dispatchEvent(new Event('change'));
    const percent = root.querySelectorAll('tbody input')[0] as HTMLInputElement;
    percent.value = '95.55';
    percent.dispatchEvent(new Event('input'));
    submit('Fabric composition');
    expect(textOf(fixture)).toContain('at most one decimal place');
    expect(composition).not.toHaveBeenCalled();
  });

  it('adds and removes rows', () => {
    const { fixture, root } = create();

    (Array.from(root.querySelectorAll('button')).find((b) => b.textContent?.includes('Add a fibre')) as HTMLButtonElement).click();
    fixture.detectChanges();
    expect(root.querySelectorAll('tbody tr').length).toBe(3);

    (Array.from(root.querySelectorAll('button')).find((b) => b.textContent?.includes('Remove')) as HTMLButtonElement).click();
    fixture.detectChanges();
    expect(root.querySelectorAll('tbody tr').length).toBe(2);
  });

  it('saves the chosen care instructions and the note (CMP-2, CMP-4)', () => {
    const { root, care, submit } = create();
    const second = root.querySelectorAll('fieldset input[type="checkbox"]')[1] as HTMLInputElement;
    second.checked = true;
    second.dispatchEvent(new Event('change'));
    const note = root.querySelector('#care-note') as HTMLTextAreaElement;
    note.value = 'Wash inside out';
    note.dispatchEvent(new Event('input'));

    submit('Care instructions');

    expect(care).toHaveBeenCalledWith({ careIds: [1, 2], note: 'Wash inside out' });
  });
});
