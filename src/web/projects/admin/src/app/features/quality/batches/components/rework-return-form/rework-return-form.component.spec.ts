import { TestBed } from '@angular/core/testing';
import { LOOKUP_NAMES_STUB } from '@app/testing/lookup-names.stub';
import { subBatch } from '@app/testing/quality-fixtures';
import { setText, textOf } from '@app/testing/production-fixtures';
import { ReworkReturnValue } from '../../models';
import { todayInPakistan } from '../../utils';
import { ReworkReturnFormComponent } from './rework-return-form.component';

describe('ReworkReturnFormComponent (spec AC-26, AC-27)', () => {
  const rework = subBatch({
    id: 62,
    splitNo: 2,
    outcomeId: 4,
    outcomeCode: 'Rework',
    statusId: 4,
    reworkRoundCount: 1,
    lines: [
      { sizeId: 2, colourId: 10, qty: 50, returnedQty: 20, outstandingQty: 30 },
      { sizeId: 1, colourId: 10, qty: 10, returnedQty: 10, outstandingQty: 0 },
    ],
  });

  function create() {
    const fixture = TestBed.createComponent(ReworkReturnFormComponent);
    fixture.componentRef.setInput('part', rework);
    fixture.componentRef.setInput('names', LOOKUP_NAMES_STUB);
    const submitted = vi.fn<(value: ReworkReturnValue) => void>();
    fixture.componentInstance.submitted.subscribe(submitted);
    fixture.detectChanges();
    return { fixture, root: fixture.nativeElement as HTMLElement, submitted };
  }

  const submit = (root: HTMLElement, fixture: { detectChanges: () => void }) => {
    (root.querySelector('form') as HTMLFormElement).dispatchEvent(new Event('submit'));
    fixture.detectChanges();
  };

  it('lists only the lines that still have pieces with the vendor, and how many (AC-27)', () => {
    const { fixture, root } = create();

    expect(root.querySelector('#rt-2-10')).not.toBeNull();
    expect(root.querySelector('#rt-1-10')).toBeNull(); // everything of this line already came back
    expect(textOf(fixture)).toContain('Still with the vendor');
    expect(textOf(fixture)).toContain('30');
  });

  it('caps the entry at what is outstanding', () => {
    const { root } = create();

    expect((root.querySelector('#rt-2-10') as HTMLInputElement).max).toBe('30');
  });

  it('emits only the lines that came back, with the part id, date and person', () => {
    const { fixture, root, submitted } = create();
    setText(root, '#rt-2-10', '25');
    setText(root, '#rt-person', 'Sara Khan');
    setText(root, '#rt-note', 'collected on Tuesday');

    submit(root, fixture);

    expect(submitted).toHaveBeenCalledWith({
      subBatchId: 62,
      returnDate: todayInPakistan(),
      actingPerson: 'Sara Khan',
      note: 'collected on Tuesday',
      lines: [{ sizeId: 2, colourId: 10, qty: 25 }],
    });
  });

  it('will not save when nothing came back', () => {
    const { fixture, root, submitted } = create();
    setText(root, '#rt-person', 'Sara Khan');

    submit(root, fixture);

    expect(submitted).not.toHaveBeenCalled();
    expect(textOf(fixture)).toContain('Enter how many came back');
  });

  it('will not save more than is outstanding, and says how many are with the vendor (AC-26)', () => {
    const { fixture, root, submitted } = create();
    setText(root, '#rt-2-10', '31');
    setText(root, '#rt-person', 'Sara Khan');

    submit(root, fixture);

    expect(submitted).not.toHaveBeenCalled();
    expect(textOf(fixture)).toContain('only 30 are still with the vendor');
  });

  it('will not save without the person', () => {
    const { fixture, root, submitted } = create();
    setText(root, '#rt-2-10', '5');

    submit(root, fixture);

    expect(submitted).not.toHaveBeenCalled();
    expect(textOf(fixture)).toContain('your name');
  });
});
