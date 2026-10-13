import { TestBed } from '@angular/core/testing';
import { LOOKUP_NAMES_STUB } from '@app/testing/lookup-names.stub';
import { buttonByText, setSelect, setText, textOf } from '@app/testing/production-fixtures';
import { AqlPlanDto, RecordInspectionValue } from '../../models';
import { todayInPakistan } from '../../utils';
import { InspectionFormComponent } from './inspection-form.component';

describe('InspectionFormComponent (spec AC-13..AC-18, AC-30)', () => {
  const lines = [
    { sizeId: 1, colourId: 10, qty: 90 },
    { sizeId: 2, colourId: 10, qty: 50 },
  ];
  const plan: AqlPlanDto = { modeId: 1, modeCode: 'Aql', codeLetter: 'F', lotQty: 140, sampleQty: 20, acceptNo: 1, rejectNo: 2 };

  function create(overrides: { plan?: AqlPlanDto | null; subBatchId?: number | null } = {}) {
    const fixture = TestBed.createComponent(InspectionFormComponent);
    fixture.componentRef.setInput('lines', lines);
    fixture.componentRef.setInput('names', LOOKUP_NAMES_STUB);
    fixture.componentRef.setInput('plan', overrides.plan === undefined ? plan : overrides.plan);
    fixture.componentRef.setInput('subBatchId', overrides.subBatchId ?? null);
    const submitted = vi.fn<(value: RecordInspectionValue) => void>();
    fixture.componentInstance.submitted.subscribe(submitted);
    fixture.detectChanges();
    return { fixture, root: fixture.nativeElement as HTMLElement, submitted };
  }

  const submit = (root: HTMLElement, fixture: { detectChanges: () => void }) => {
    (root.querySelector('form') as HTMLFormElement).dispatchEvent(new Event('submit'));
    fixture.detectChanges();
  };

  it('says what the AQL plan will apply before anything is entered (AC-13)', () => {
    const { fixture } = create();

    const text = textOf(fixture);

    expect(text).toContain('AQL sampling');
    expect(text).toContain('Inspect 20 of the 140 pieces (code letter F)');
    expect(text).toContain('Up to 1 major or minor defects passes; 2 or more fails');
  });

  it('says a small lot is inspected 100% (AC-14)', () => {
    const { fixture } = create({ plan: { ...plan, modeCode: 'Full', lotQty: 40, sampleQty: 40 } });

    expect(textOf(fixture)).toContain('inspect all 40 pieces');
  });

  it('lists the six tech-pack checks, all starting as Pass (AC-16)', () => {
    const { root } = create();

    const selects = Array.from(root.querySelectorAll<HTMLSelectElement>('select[id^="chk-"]'));

    expect(selects).toHaveLength(6);
    expect(selects.every((s) => s.value === '1')).toBe(true);
  });

  it('emits all six results, the date and the person with no defects (AC-16, AC-30)', () => {
    const { fixture, root, submitted } = create();
    setText(root, '#insp-person', 'Ali Raza');

    submit(root, fixture);

    const value = submitted.mock.calls[0][0];
    expect(value.checks).toHaveLength(6);
    expect(value.checks.every((c) => c.resultId === 1)).toBe(true);
    expect(value.defects).toEqual([]);
    expect(value.subBatchId).toBeNull();
    expect(value.inspectionDate).toBe(todayInPakistan());
    expect(value.actingPerson).toBe('Ali Raza');
    expect(value.sampleQty).toBeNull();
  });

  it('will not save without the person, and names what is missing', () => {
    const { fixture, root, submitted } = create();

    submit(root, fixture);

    expect(submitted).not.toHaveBeenCalled();
    expect(textOf(fixture)).toContain("This can't be saved yet");
  });

  it('needs a defect of the matching type for a failed check (AC-17)', () => {
    const { fixture, root, submitted } = create();
    setText(root, '#insp-person', 'Ali Raza');
    setSelect(root, '#chk-1', 1); // stitching fails

    submit(root, fixture);

    expect(submitted).not.toHaveBeenCalled();
    expect(textOf(fixture)).toContain('"Stitching" failed, so add at least one defect of that type');
  });

  it('needs a written note on every defect (AC-17)', () => {
    const { fixture, root, submitted } = create();
    setText(root, '#insp-person', 'Ali Raza');
    buttonByText(root, 'Add a defect').click();
    fixture.detectChanges();

    submit(root, fixture);

    expect(submitted).not.toHaveBeenCalled();
    expect(textOf(fixture)).toContain('written note');
  });

  it('emits a defect with its type, severity, quantity, size and colour and note (AC-17)', () => {
    const { fixture, root, submitted } = create();
    setText(root, '#insp-person', 'Ali Raza');
    setSelect(root, '#chk-1', 1);
    buttonByText(root, 'Add a defect').click();
    fixture.detectChanges();
    setSelect(root, '#df-type-1', 0); // Stitching
    setSelect(root, '#df-sev-1', 1); // Major
    setText(root, '#df-qty-1', '3');
    setSelect(root, '#df-line-1', 1); // Size 1 / Colour 10
    setText(root, '#df-note-1', 'loose thread at hem');

    submit(root, fixture);

    expect(submitted.mock.calls[0][0].defects).toEqual([
      { defectTypeId: 1, severityId: 2, qty: 3, sizeId: 1, colourId: 10, note: 'loose thread at hem' },
    ]);
    expect(submitted.mock.calls[0][0].checks.find((c) => c.checkTypeId === 1)?.resultId).toBe(2);
  });

  it('removes a defect that was added by mistake', () => {
    const { fixture, root } = create();
    buttonByText(root, 'Add a defect').click();
    fixture.detectChanges();

    buttonByText(root, 'Remove defect 1').click();
    fixture.detectChanges();

    expect(textOf(fixture)).toContain('No defects recorded');
  });

  it('is headed as a re-inspection when it is for a returned part (AC-26)', () => {
    const { fixture, root, submitted } = create({ subBatchId: 62 });
    setText(root, '#insp-person', 'Ali Raza');

    submit(root, fixture);

    expect(textOf(fixture)).toContain('Re-inspect the returned part');
    expect(submitted.mock.calls[0][0].subBatchId).toBe(62);
  });

  it('shows an API refusal in plain words', () => {
    const { fixture } = create();
    fixture.componentRef.setInput('error', { status: 409, message: 'This batch has already been inspected.', fieldErrors: {} });
    fixture.detectChanges();

    expect(textOf(fixture)).toContain('already been inspected');
  });
});
