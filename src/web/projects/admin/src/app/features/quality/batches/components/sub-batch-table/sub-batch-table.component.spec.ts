import { TestBed } from '@angular/core/testing';
import { LOOKUP_NAMES_STUB } from '@app/testing/lookup-names.stub';
import { bothSafetyPassed, inspection, qcBatch, subBatch } from '@app/testing/quality-fixtures';
import { buttonByText, textOf } from '@app/testing/production-fixtures';
import { QcBatchDto } from '../../models';
import { SubBatchTableComponent } from './sub-batch-table.component';
import { provideAllowAllPermissions } from '@app/testing/provide-allow-all-permissions';

describe('SubBatchTableComponent (spec AC-20..AC-27, AC-44)', () => {
  beforeEach(() => TestBed.configureTestingModule({ providers: [provideAllowAllPermissions()] }));
  function create(batch: QcBatchDto) {
    const fixture = TestBed.createComponent(SubBatchTableComponent);
    fixture.componentRef.setInput('batch', batch);
    fixture.componentRef.setInput('names', LOOKUP_NAMES_STUB);
    const events = { returned: vi.fn(), reinspect: vi.fn(), decide: vi.fn(), safety: vi.fn() };
    fixture.componentInstance.returnRequested.subscribe(events.returned);
    fixture.componentInstance.reinspectRequested.subscribe(events.reinspect);
    fixture.componentInstance.decideRequested.subscribe(events.decide);
    fixture.componentInstance.safetyRequested.subscribe(events.safety);
    fixture.detectChanges();
    return { fixture, root: fixture.nativeElement as HTMLElement, events };
  }

  const accepted = subBatch({ id: 61, splitNo: 1 });
  const rejected = subBatch({ id: 62, splitNo: 2, outcomeId: 3, outcomeCode: 'Rejected', reason: 'torn seams', lines: [{ sizeId: 2, colourId: 10, qty: 20, returnedQty: 0, outstandingQty: 0 }] });
  const rework = subBatch({
    id: 63,
    splitNo: 3,
    outcomeId: 4,
    outcomeCode: 'Rework',
    reason: 're-sew hems',
    statusId: 4,
    reworkRoundCount: 1,
    lines: [{ sizeId: 2, colourId: 10, qty: 30, returnedQty: 10, outstandingQty: 20 }],
  });

  it('shows every part with its outcome in words, the reason, the rounds and what is still outstanding', () => {
    const { fixture } = create(qcBatch({ statusId: 4, subBatches: [accepted, rejected, rework] }));

    const text = textOf(fixture);

    expect(text).toContain('Accepted');
    expect(text).toContain('Rejected');
    expect(text).toContain('torn seams');
    expect(text).toContain('Sent back for rework');
    expect(text).toContain('20 pieces'); // still with the vendor
  });

  it('offers to record a return only for a rework part that still has pieces out (AC-26)', () => {
    const { root, events } = create(qcBatch({ statusId: 4, subBatches: [accepted, rework] }));

    buttonByText(root, 'Record return').click();

    expect(events.returned).toHaveBeenCalledWith(63);
    expect(root.querySelectorAll('button')).toHaveLength(1);
  });

  it('offers a re-inspection for a returned part that is waiting for one', () => {
    const returned = subBatch({ id: 64, splitNo: 4, parentSubBatchId: 63, outcomeId: null, outcomeCode: null, statusId: 1, reworkRoundCount: 1 });
    const { root, events, fixture } = create(qcBatch({ statusId: 4, subBatches: [rework, returned] }));

    buttonByText(root, 'Re-inspect').click();

    expect(events.reinspect).toHaveBeenCalledWith(64);
    expect(textOf(fixture)).toContain('came back from rework');
  });

  it('asks for the safety checks of a re-inspected part before its outcome (AC-34)', () => {
    const returned = subBatch({ id: 64, splitNo: 4, parentSubBatchId: 63, outcomeId: null, outcomeCode: null, statusId: 2 });
    const batch = qcBatch({ statusId: 4, subBatches: [returned], inspections: [inspection({ id: 70, subBatchId: 64, roundNo: 2 })] });
    const { root, events } = create(batch);

    buttonByText(root, 'Record safety checks').click();
    buttonByText(root, 'Record outcome').click();

    expect(events.safety).toHaveBeenCalledWith(70);
    expect(events.decide).toHaveBeenCalledWith(64);
  });

  it('does not ask for safety checks again once they are recorded on the re-inspection', () => {
    const returned = subBatch({ id: 64, splitNo: 4, parentSubBatchId: 63, outcomeId: null, outcomeCode: null, statusId: 2 });
    const batch = qcBatch({
      statusId: 4,
      subBatches: [returned],
      inspections: [inspection({ id: 70, subBatchId: 64, roundNo: 2, safetyChecks: bothSafetyPassed() })],
    });

    const { root } = create(batch);

    expect(Array.from(root.querySelectorAll('button')).some((b) => (b.textContent ?? '').includes('safety'))).toBe(false);
    expect(buttonByText(root, 'Record outcome')).toBeTruthy();
  });

  it('offers no action on a part that is already decided (AC-25)', () => {
    const { root } = create(qcBatch({ statusId: 3, subBatches: [accepted, rejected] }));

    expect(root.querySelectorAll('button')).toHaveLength(0);
  });
});
