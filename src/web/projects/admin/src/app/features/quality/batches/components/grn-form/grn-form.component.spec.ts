import { TestBed } from '@angular/core/testing';
import { LOOKUP_NAMES_STUB } from '@app/testing/lookup-names.stub';
import { inboundDelivery } from '@app/testing/quality-fixtures';
import { setText, textOf } from '@app/testing/production-fixtures';
import { todayInPakistan } from '../../utils';
import { RecordReceiptValue } from '../../models';
import { GrnFormComponent } from './grn-form.component';

describe('GrnFormComponent (spec AC-3, AC-6, AC-7, AC-8, AC-11)', () => {
  function create(delivery = inboundDelivery(), defaultPerson = '') {
    const fixture = TestBed.createComponent(GrnFormComponent);
    fixture.componentRef.setInput('delivery', delivery);
    fixture.componentRef.setInput('names', LOOKUP_NAMES_STUB);
    fixture.componentRef.setInput('defaultPerson', defaultPerson);
    const submitted = vi.fn<(value: RecordReceiptValue) => void>();
    fixture.componentInstance.submitted.subscribe(submitted);
    fixture.detectChanges();
    return { fixture, root: fixture.nativeElement as HTMLElement, submitted };
  }

  const submit = (root: HTMLElement, fixture: { detectChanges: () => void }) => {
    (root.querySelector('form') as HTMLFormElement).dispatchEvent(new Event('submit'));
    fixture.detectChanges();
  };

  it('starts every counted quantity at what the vendor claims, so the usual case is one click', () => {
    const { root } = create();

    expect((root.querySelector('#grn-1-10') as HTMLInputElement).value).toBe('100');
    expect((root.querySelector('#grn-2-10') as HTMLInputElement).value).toBe('50');
  });

  it('shows the reconciliation as it is typed, in words', () => {
    const { fixture, root } = create();

    expect(textOf(fixture)).toContain('Matched the delivery note');

    setText(root, '#grn-1-10', '90');
    fixture.detectChanges();
    expect(textOf(fixture)).toContain('Short by 10');
    expect(textOf(fixture)).toContain('140 counted of 150 claimed');

    setText(root, '#grn-1-10', '104');
    fixture.detectChanges();
    expect(textOf(fixture)).toContain('Over by 4');
  });

  it('bounds the date from the dispatch date to today and starts on today', () => {
    const { root } = create(inboundDelivery({ dispatchDate: '2026-09-20' }));

    const date = root.querySelector('#grn-date') as HTMLInputElement;

    expect(date.min).toBe('2026-09-20');
    expect(date.max).toBe(todayInPakistan());
    expect(date.value).toBe(todayInPakistan());
  });

  it('starts the name field with the name last used in this browser', () => {
    expect((create(inboundDelivery(), 'Sara Khan').root.querySelector('#grn-person') as HTMLInputElement).value).toBe('Sara Khan');
  });

  it('will not save without the person: it names what is missing and emits nothing (AC-8)', () => {
    const { fixture, root, submitted } = create();

    submit(root, fixture);

    expect(submitted).not.toHaveBeenCalled();
    expect(textOf(fixture)).toContain("This can't be saved yet");
    expect(textOf(fixture)).toContain('your name');
  });

  it('emits the counted lines with the delivery id, the date and the trimmed person', () => {
    const { fixture, root, submitted } = create();
    setText(root, '#grn-1-10', '96');
    setText(root, '#grn-person', '  Sara Khan ');
    setText(root, '#grn-note', 'two cartons damaged');

    submit(root, fixture);

    expect(submitted).toHaveBeenCalledWith({
      inboundDeliveryId: 11,
      receivedDate: todayInPakistan(),
      actingPerson: 'Sara Khan',
      note: 'two cartons damaged',
      lines: [
        { sizeId: 1, colourId: 10, qty: 96 },
        { sizeId: 2, colourId: 10, qty: 50 },
      ],
    });
  });

  it('never turns a negative or blank count into a negative quantity', () => {
    const { fixture, root, submitted } = create();
    setText(root, '#grn-1-10', '-5');
    setText(root, '#grn-2-10', '');
    setText(root, '#grn-person', 'Sara Khan');

    submit(root, fixture);

    expect(submitted.mock.calls[0][0].lines.map((l) => l.qty)).toEqual([0, 0]);
  });

  it('shows the API refusal in plain words next to the form', () => {
    const { fixture } = create();
    fixture.componentRef.setInput('error', { status: 409, message: 'Delivery note DN-1 already has a goods receipt.', fieldErrors: {} });
    fixture.detectChanges();

    expect(textOf(fixture)).toContain('already has a goods receipt');
  });
});
