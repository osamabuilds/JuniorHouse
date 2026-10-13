import { TestBed } from '@angular/core/testing';
import { LOOKUP_NAMES_STUB } from '@app/testing/lookup-names.stub';
import { buttonByText, quantities, setText, textOf } from '@app/testing/production-fixtures';
import { DeliveryNoteValue } from '../../models';
import { todayInPakistan } from '../../utils';
import { DeliveryNoteFormComponent } from './delivery-note-form.component';

describe('DeliveryNoteFormComponent (spec AC-18..AC-24, D3, D4)', () => {
  function create(defaultPerson = '') {
    const fixture = TestBed.createComponent(DeliveryNoteFormComponent);
    fixture.componentRef.setInput('quantities', quantities());
    fixture.componentRef.setInput('names', LOOKUP_NAMES_STUB);
    fixture.componentRef.setInput('minDate', '2026-09-01');
    fixture.componentRef.setInput('defaultPerson', defaultPerson);
    const submitted = vi.fn();
    fixture.componentInstance.submitted.subscribe(submitted);
    fixture.detectChanges();
    const root = fixture.nativeElement as HTMLElement;
    const submit = () => {
      (root.querySelector('form') as HTMLFormElement).dispatchEvent(new Event('submit'));
      fixture.detectChanges();
    };
    return { fixture, root, submitted, submit };
  }

  it('shows what was ordered and what has already shipped for each size and colour (AC-19)', () => {
    const { root } = create();

    const rows = Array.from(root.querySelectorAll('tbody tr')).map((row) => (row.textContent ?? '').replace(/\s+/g, ' ').trim());

    expect(rows[0]).toContain('Size 1');
    expect(rows[0]).toContain('100');
    expect(rows[1]).toContain('Size 2');
  });

  it('bounds the dispatch date between the day the PO was sent and today (AC-18)', () => {
    const { root } = create();

    const date = root.querySelector('#dn-date') as HTMLInputElement;

    expect(date.min).toBe('2026-09-01');
    expect(date.max).toBe(todayInPakistan());
  });

  it('will not save without a number, a name and at least one piece, and says so', () => {
    const { fixture, submitted, submit } = create();

    submit();

    expect(submitted).not.toHaveBeenCalled();
    const text = textOf(fixture);
    expect(text).toContain('the delivery note number');
    expect(text).toContain('your name');
    expect(text).toContain('at least one piece in the shipment');
  });

  it('emits the note with only the sizes that have pieces, whole numbers only', () => {
    const { root, submitted, submit } = create();
    setText(root, '#dn-no', ' DN-100 ');
    setText(root, '#dn-person', 'Osama Shafique');
    setText(root, '#dn-qty-1-10', '60');
    setText(root, '#dn-qty-2-10', '0');

    submit();

    const value = submitted.mock.calls[0][0] as DeliveryNoteValue;
    expect(value).toMatchObject({ vendorDnNo: 'DN-100', isFinalShipment: false, actingPerson: 'Osama Shafique' });
    expect(value.lines).toEqual([{ sizeId: 1, colourId: 10, qty: 60 }]);
  });

  it('carries the final-shipment box (D3)', () => {
    const { root, submitted, submit } = create('Osama Shafique');
    setText(root, '#dn-no', 'DN-101');
    setText(root, '#dn-qty-1-10', '20');
    const final = root.querySelector('#dn-final') as HTMLInputElement;
    final.checked = true;
    final.dispatchEvent(new Event('change'));

    submit();

    expect((submitted.mock.calls[0][0] as DeliveryNoteValue).isFinalShipment).toBe(true);
  });

  it('does not stop the person entering more than the allowance: extra pieces are recorded and flagged by the API (D4)', () => {
    const { root, submitted, submit } = create('Osama Shafique');
    setText(root, '#dn-no', 'DN-102');
    setText(root, '#dn-qty-1-10', '999');

    submit();

    expect((submitted.mock.calls[0][0] as DeliveryNoteValue).lines[0].qty).toBe(999);
  });

  it('shows the API refusal, such as a duplicate delivery note number, and disables the button while saving', () => {
    const { fixture, root } = create();
    fixture.componentRef.setInput('error', { status: 409, message: 'Delivery note DN-7 has already been recorded for PO PO-2026-00010.', fieldErrors: {} });
    fixture.componentRef.setInput('saving', true);
    fixture.detectChanges();

    expect(textOf(fixture)).toContain('has already been recorded');
    expect(buttonByText(root, 'Saving').disabled).toBe(true);
  });

  it('labels the number, date, name, final box and every quantity box', () => {
    const { root } = create();

    for (const id of ['dn-no', 'dn-date', 'dn-person', 'dn-final', 'dn-qty-1-10', 'dn-qty-2-10']) {
      expect(root.querySelector(`label[for="${id}"]`), id).not.toBeNull();
    }
  });
});
