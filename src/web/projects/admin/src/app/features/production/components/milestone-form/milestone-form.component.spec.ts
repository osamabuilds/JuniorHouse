import { TestBed } from '@angular/core/testing';
import { LOOKUP_NAMES_STUB } from '@app/testing/lookup-names.stub';
import { buttonByText, runDetail, setSelect, setText, textOf } from '@app/testing/production-fixtures';
import { RecordMilestoneValue } from '../../models';
import { todayInPakistan } from '../../utils';
import { MilestoneFormComponent } from './milestone-form.component';

describe('MilestoneFormComponent (spec AC-4..AC-8, AC-17, AC-35)', () => {
  function create(run = runDetail(), defaultPerson = '') {
    const fixture = TestBed.createComponent(MilestoneFormComponent);
    fixture.componentRef.setInput('run', run);
    fixture.componentRef.setInput('names', LOOKUP_NAMES_STUB);
    fixture.componentRef.setInput('defaultPerson', defaultPerson);
    const submitted = vi.fn();
    fixture.componentInstance.submitted.subscribe(submitted);
    fixture.detectChanges();
    return { fixture, root: fixture.nativeElement as HTMLElement, submitted };
  }

  const submit = (root: HTMLElement, fixture: { detectChanges: () => void }) => {
    (root.querySelector('form') as HTMLFormElement).dispatchEvent(new Event('submit'));
    fixture.detectChanges();
  };

  it('lists only the milestones that apply to this run', () => {
    const { root } = create(runDetail({ fabricResponsibilityId: 2 }));

    const options = Array.from(root.querySelectorAll('#ms-type option')).map((option) => (option.textContent ?? '').trim());

    expect(options.some((text) => text.startsWith('Fabric dispatched by Romp'))).toBe(true);
    expect(options.some((text) => text.startsWith('Fabric booked'))).toBe(false);
  });

  it('greys out bulk cutting with the reason until a sample is approved (AC-9)', () => {
    const { root } = create(runDetail({ hasApprovedSample: false }));

    const cutting = Array.from(root.querySelectorAll<HTMLOptionElement>('#ms-type option')).find((option) => (option.textContent ?? '').includes('Bulk cutting'));

    expect(cutting?.disabled).toBe(true);
    expect(cutting?.textContent).toContain('needs an approved pre-production sample first');
  });

  it('bounds the date between the day the PO was sent and today', () => {
    const { root } = create(runDetail({ poSentDate: '2026-09-01' }));

    const date = root.querySelector('#ms-date') as HTMLInputElement;

    expect(date.min).toBe('2026-09-01');
    expect(date.max).toBe(todayInPakistan());
    expect(date.value).toBe(todayInPakistan());
  });

  it('starts the name field with the name last used in this browser', () => {
    const { root } = create(runDetail(), 'Osama Shafique');

    expect((root.querySelector('#ms-person') as HTMLInputElement).value).toBe('Osama Shafique');
  });

  it('will not save an empty form: it names what is missing and emits nothing (AC-35)', () => {
    const { fixture, root, submitted } = create();

    submit(root, fixture);

    expect(submitted).not.toHaveBeenCalled();
    const text = textOf(fixture);
    expect(text).toContain("This can't be saved yet");
    expect(text).toContain('your name');
    expect(text).toContain('vendor contact');
  });

  it('rejects a one-letter name', () => {
    const { fixture, root, submitted } = create();
    setSelect(root, '#ms-type', 1);
    setText(root, '#ms-reporter', 'Ali Raza');
    setText(root, '#ms-person', 'O');

    submit(root, fixture);

    expect(submitted).not.toHaveBeenCalled();
  });

  it('emits the recorded values, trimmed', () => {
    const { fixture, root, submitted } = create();
    setSelect(root, '#ms-type', 1);
    setText(root, '#ms-reporter', '  Ali Raza ');
    setText(root, '#ms-person', ' Osama Shafique ');
    setText(root, '#ms-note', 'fabric in the warehouse');

    submit(root, fixture);

    expect(submitted).toHaveBeenCalledOnce();
    const value = submitted.mock.calls[0][0] as RecordMilestoneValue;
    expect(value).toMatchObject({ typeId: 1, reportedBy: 'Ali Raza', actingPerson: 'Osama Shafique', note: 'fabric in the warehouse', finishedLines: null });
    expect(value.claimedDate).toBe(todayInPakistan());
  });

  it('asks for finished quantities only for finished-and-ready, and leaves out zero rows', () => {
    const { fixture, root, submitted } = create(runDetail({ cuttingStartedDate: '2026-09-10', hasApprovedSample: true }));
    setSelect(root, '#ms-type', 2); // Finished and ready to ship
    fixture.detectChanges();

    expect(root.querySelectorAll('#ms-fin-1-10').length).toBe(1);
    setText(root, '#ms-fin-1-10', '80');
    setText(root, '#ms-reporter', 'Ali Raza');
    setText(root, '#ms-person', 'Osama Shafique');
    submit(root, fixture);

    const value = submitted.mock.calls[0][0] as RecordMilestoneValue;
    expect(value.typeId).toBe(7);
    expect(value.finishedLines).toEqual([{ sizeId: 1, colourId: 10, qty: 80 }]);
  });

  it('shows the API refusal in words and disables the button while saving', () => {
    const { fixture, root } = create();
    fixture.componentRef.setInput('error', { status: 409, message: 'The date must be between 2026-09-01 and today.', fieldErrors: { ClaimedDate: ['Date out of range.'] } });
    fixture.componentRef.setInput('saving', true);
    fixture.detectChanges();

    expect(textOf(fixture)).toContain('The date must be between 2026-09-01 and today.');
    expect(textOf(fixture)).toContain('Date out of range.');
    expect(buttonByText(root, 'Saving').disabled).toBe(true);
  });

  it('labels every control (semantic form)', () => {
    const { root } = create();

    for (const id of ['ms-type', 'ms-date', 'ms-reporter', 'ms-person', 'ms-note']) {
      expect(root.querySelector(`label[for="${id}"]`), id).not.toBeNull();
    }
  });
});
