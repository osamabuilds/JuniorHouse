import { TestBed } from '@angular/core/testing';
import { subBatch } from '@app/testing/quality-fixtures';
import { setText, textOf } from '@app/testing/production-fixtures';
import { DecideSubBatchValue } from '../../models';
import { PartDecisionFormComponent } from './part-decision-form.component';

describe('PartDecisionFormComponent (spec AC-26, AC-34)', () => {
  const part = subBatch({ id: 63, splitNo: 3, parentSubBatchId: 62, outcomeId: null, outcomeCode: null, statusId: 2, reworkRoundCount: 1 });

  function create() {
    const fixture = TestBed.createComponent(PartDecisionFormComponent);
    fixture.componentRef.setInput('part', part);
    const submitted = vi.fn<(value: DecideSubBatchValue) => void>();
    fixture.componentInstance.submitted.subscribe(submitted);
    fixture.detectChanges();
    return { fixture, root: fixture.nativeElement as HTMLElement, submitted };
  }

  const submit = (root: HTMLElement, fixture: { detectChanges: () => void }) => {
    (root.querySelector('form') as HTMLFormElement).dispatchEvent(new Event('submit'));
    fixture.detectChanges();
  };

  it('names the part and the rework round it came back from', () => {
    expect(textOf(create().fixture)).toContain('Outcome for returned part 3');
    expect(textOf(create().fixture)).toContain('round 2');
  });

  it('records an acceptance without a reason', () => {
    const { fixture, root, submitted } = create();
    setText(root, '#pd-person', 'Ali Raza');

    submit(root, fixture);

    expect(submitted).toHaveBeenCalledWith({ subBatchId: 63, outcomeId: 1, reason: null, actingPerson: 'Ali Raza' });
  });

  it('needs a reason for anything but Accept (AC-20)', () => {
    const { fixture, root, submitted } = create();
    setText(root, '#pd-person', 'Ali Raza');
    const select = root.querySelector('#pd-outcome') as HTMLSelectElement;
    select.selectedIndex = 3; // Rework again: rounds are unlimited
    select.dispatchEvent(new Event('change'));
    fixture.detectChanges();

    expect(textOf(fixture)).toContain('Reason (required)');
    submit(root, fixture);
    expect(submitted).not.toHaveBeenCalled();
    expect(textOf(fixture)).toContain('Give a reason');

    setText(root, '#pd-reason', 'still crooked');
    submit(root, fixture);
    expect(submitted).toHaveBeenCalledWith({ subBatchId: 63, outcomeId: 4, reason: 'still crooked', actingPerson: 'Ali Raza' });
  });

  it('will not save without the person', () => {
    const { fixture, root, submitted } = create();

    submit(root, fixture);

    expect(submitted).not.toHaveBeenCalled();
    expect(textOf(fixture)).toContain('your name');
  });
});
