import { TestBed } from '@angular/core/testing';
import { LOOKUP_NAMES_STUB } from '@app/testing/lookup-names.stub';
import { buttonByText, setSelect, setText, textOf } from '@app/testing/production-fixtures';
import { DecideBatchValue } from '../../models';
import { OutcomePanelComponent } from './outcome-panel.component';

describe('OutcomePanelComponent (spec AC-19..AC-25, AC-31)', () => {
  const lines = [
    { sizeId: 1, colourId: 10, qty: 90 },
    { sizeId: 2, colourId: 10, qty: 50 },
  ];

  function create(safetyRecorded = true) {
    const fixture = TestBed.createComponent(OutcomePanelComponent);
    fixture.componentRef.setInput('lines', lines);
    fixture.componentRef.setInput('names', LOOKUP_NAMES_STUB);
    fixture.componentRef.setInput('safetyRecorded', safetyRecorded);
    const submitted = vi.fn<(value: DecideBatchValue) => void>();
    fixture.componentInstance.submitted.subscribe(submitted);
    fixture.detectChanges();
    return { fixture, root: fixture.nativeElement as HTMLElement, submitted };
  }

  const submit = (root: HTMLElement, fixture: { detectChanges: () => void }) => {
    (root.querySelector('form') as HTMLFormElement).dispatchEvent(new Event('submit'));
    fixture.detectChanges();
  };

  const chooseSplit = (root: HTMLElement, fixture: { detectChanges: () => void }) => {
    (root.querySelector('input[type="radio"][value="split"]') as HTMLInputElement).dispatchEvent(new Event('change'));
    fixture.detectChanges();
  };

  const submitButton = (root: HTMLElement) => root.querySelector('button[type="submit"]') as HTMLButtonElement;

  it('offers the four outcomes in plain words (AC-20)', () => {
    const { root } = create();

    const options = Array.from(root.querySelectorAll('#oc-out-1 option')).map((o) => (o.textContent ?? '').trim());

    expect(options).toEqual(['Accept', 'Accept with concession', 'Reject and return to vendor', 'Rework (send back to the vendor)']);
  });

  it('records the whole batch in one part, without listing lines, with the person', () => {
    const { fixture, root, submitted } = create();
    setText(root, '#oc-person', 'Ali Raza');

    submit(root, fixture);

    expect(submitted).toHaveBeenCalledWith({ actingPerson: 'Ali Raza', parts: [{ outcomeId: 1, reason: null, lines: null }] });
  });

  it('asks for a reason except for Accept, and switches the button off until it is given (AC-20)', () => {
    const { fixture, root } = create();
    expect(textOf(fixture)).toContain('Reason (optional)');

    setSelect(root, '#oc-out-1', 2); // Reject
    fixture.detectChanges();

    expect(textOf(fixture)).toContain('Reason (required)');
    expect(submitButton(root).disabled).toBe(true);

    setText(root, '#oc-reason-1', 'wrong fabric');
    fixture.detectChanges();
    expect(submitButton(root).disabled).toBe(false);
  });

  it('warns that nothing can be accepted until both safety checks are recorded (AC-31)', () => {
    expect(textOf(create(false).fixture)).toContain('Both child-safety checks must be recorded before anything can be accepted');
    expect(textOf(create(true).fixture)).not.toContain('Both child-safety checks must be recorded');
  });

  it('a split starts with a passing part holding everything and an empty second part (AC-22, AC-23)', () => {
    const { fixture, root } = create();

    chooseSplit(root, fixture);

    expect((root.querySelector('#oc-q-2-1-10') as HTMLInputElement).value).toBe('90');
    expect((root.querySelector('#oc-q-3-1-10') as HTMLInputElement).value).toBe('0');
    expect(textOf(fixture)).toContain('Part 1');
    expect(textOf(fixture)).toContain('Part 2');
  });

  it('shows what is still to place, and keeps the button off until every piece has a part (AC-22)', () => {
    const { fixture, root } = create();
    chooseSplit(root, fixture);
    setSelect(root, '#oc-out-3', 2);
    setText(root, '#oc-reason-3', 'torn seams');

    // Move 10 of size 2 into part 2, but forget 5 of them.
    setText(root, '#oc-q-2-2-10', '40');
    setText(root, '#oc-q-3-2-10', '5');
    fixture.detectChanges();

    expect(textOf(fixture)).toContain('5 still to place');
    expect(submitButton(root).disabled).toBe(true);

    setText(root, '#oc-q-3-2-10', '10');
    fixture.detectChanges();
    expect(textOf(fixture)).toContain('All placed');
    expect(submitButton(root).disabled).toBe(false);
  });

  it('shows one unit too many as "too many", not as fine (AC-22)', () => {
    const { fixture, root } = create();
    chooseSplit(root, fixture);
    setText(root, '#oc-q-3-1-10', '1');
    fixture.detectChanges();

    expect(textOf(fixture)).toContain('1 too many');
    expect(submitButton(root).disabled).toBe(true);
  });

  it('emits a split with only the lines each part holds (AC-22, AC-23)', () => {
    const { fixture, root, submitted } = create();
    setText(root, '#oc-person', 'Ali Raza');
    chooseSplit(root, fixture);
    setSelect(root, '#oc-out-3', 2);
    setText(root, '#oc-reason-3', 'torn seams');
    setText(root, '#oc-q-2-2-10', '30');
    setText(root, '#oc-q-3-2-10', '20');
    fixture.detectChanges();

    submit(root, fixture);

    expect(submitted).toHaveBeenCalledWith({
      actingPerson: 'Ali Raza',
      parts: [
        { outcomeId: 1, reason: null, lines: [{ sizeId: 1, colourId: 10, qty: 90 }, { sizeId: 2, colourId: 10, qty: 30 }] },
        { outcomeId: 3, reason: 'torn seams', lines: [{ sizeId: 2, colourId: 10, qty: 20 }] },
      ],
    });
  });

  it('can add another part, and only removes a part beyond the two a split needs', () => {
    const { fixture, root } = create();
    chooseSplit(root, fixture);
    expect(Array.from(root.querySelectorAll('button')).some((b) => (b.textContent ?? '').includes('Remove part'))).toBe(false);

    buttonByText(root, 'Add another part').click();
    fixture.detectChanges();
    expect(textOf(fixture)).toContain('Part 3');

    buttonByText(root, 'Remove part 3').click();
    fixture.detectChanges();
    expect(textOf(fixture)).not.toContain('Part 3');
  });

  it('will not save without the person', () => {
    const { fixture, root, submitted } = create();

    submit(root, fixture);

    expect(submitted).not.toHaveBeenCalled();
    expect(textOf(fixture)).toContain('your name');
  });

  it('shows an API refusal in plain words', () => {
    const { fixture } = create();
    fixture.componentRef.setInput('error', { status: 409, message: 'Record the child-safety check for choking hazard first.', fieldErrors: {} });
    fixture.detectChanges();

    expect(textOf(fixture)).toContain('Record the child-safety check');
  });
});
