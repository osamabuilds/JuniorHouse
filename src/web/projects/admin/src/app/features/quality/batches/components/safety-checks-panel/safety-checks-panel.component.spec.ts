import { TestBed } from '@angular/core/testing';
import { LOOKUP_NAMES_STUB } from '@app/testing/lookup-names.stub';
import { bothSafetyPassed, inspection } from '@app/testing/quality-fixtures';
import { buttonByText, click, setSelect, setText, textOf } from '@app/testing/production-fixtures';
import { InspectionDto, RecordSafetyValue } from '../../models';
import { SafetyChecksPanelComponent } from './safety-checks-panel.component';

describe('SafetyChecksPanelComponent (spec AC-31..AC-34, BRD 5.11)', () => {
  const lines = [
    { sizeId: 1, colourId: 10, qty: 90 },
    { sizeId: 2, colourId: 10, qty: 50 },
  ];

  function create(target: InspectionDto = inspection(), canChange = true) {
    const fixture = TestBed.createComponent(SafetyChecksPanelComponent);
    fixture.componentRef.setInput('inspection', target);
    fixture.componentRef.setInput('lines', lines);
    fixture.componentRef.setInput('names', LOOKUP_NAMES_STUB);
    fixture.componentRef.setInput('canChange', canChange);
    const submitted = vi.fn<(value: RecordSafetyValue) => void>();
    fixture.componentInstance.submitted.subscribe(submitted);
    fixture.detectChanges();
    return { fixture, root: fixture.nativeElement as HTMLElement, submitted };
  }

  const submit = (root: HTMLElement, fixture: { detectChanges: () => void }) => {
    (root.querySelector('form') as HTMLFormElement).dispatchEvent(new Event('submit'));
    fixture.detectChanges();
  };

  it('asks for both checks by name, saying nothing can be accepted without them', () => {
    const { fixture } = create();

    const text = textOf(fixture);

    expect(text).toContain('Choking hazard on small trims');
    expect(text).toContain('Drawstring safety on necklines');
    expect(text).toContain('Both checks are required before anything can be accepted');
  });

  it('records both as Pass with the person (AC-30, AC-31)', () => {
    const { fixture, root, submitted } = create();
    setText(root, '#sf-person', 'Ali Raza');

    submit(root, fixture);

    expect(submitted).toHaveBeenCalledWith({
      inspectionId: 51,
      actingPerson: 'Ali Raza',
      checks: [
        { safetyTypeId: 1, resultId: 1, reason: null, affectedLines: [] },
        { safetyTypeId: 2, resultId: 1, reason: null, affectedLines: [] },
      ],
    });
  });

  it('will not save without the person', () => {
    const { fixture, root, submitted } = create();

    submit(root, fixture);

    expect(submitted).not.toHaveBeenCalled();
    expect(textOf(fixture)).toContain('your name');
  });

  it('asks for a reason when a check does not apply, and refuses to save without one (AC-33)', () => {
    const { fixture, root, submitted } = create();
    setText(root, '#sf-person', 'Ali Raza');
    setSelect(root, '#sf-result-1', 2); // Not applicable
    fixture.detectChanges();

    expect(root.querySelector('#sf-reason-1')).not.toBeNull();
    submit(root, fixture);
    expect(submitted).not.toHaveBeenCalled();
    expect(textOf(fixture)).toContain('Say why "Choking hazard on small trims" does not apply');

    setText(root, '#sf-reason-1', 'no small trims on this garment');
    submit(root, fixture);
    expect(submitted.mock.calls[0][0].checks[0]).toEqual({ safetyTypeId: 1, resultId: 3, reason: 'no small trims on this garment', affectedLines: [] });
  });

  it('lets a failed check name the sizes and colours it applies to (AC-32)', () => {
    const { fixture, root, submitted } = create();
    setText(root, '#sf-person', 'Ali Raza');
    setSelect(root, '#sf-result-2', 1); // Fail
    fixture.detectChanges();

    const boxes = Array.from(root.querySelectorAll<HTMLInputElement>('fieldset fieldset input[type="checkbox"]'));
    expect(boxes).toHaveLength(2);
    boxes[1].checked = true;
    boxes[1].dispatchEvent(new Event('change'));
    fixture.detectChanges();
    submit(root, fixture);

    expect(submitted.mock.calls[0][0].checks[1]).toEqual({ safetyTypeId: 2, resultId: 2, reason: null, affectedLines: [{ sizeId: 2, colourId: 10 }] });
  });

  it('a failure with no lines ticked applies to the whole batch (Q10)', () => {
    const { fixture, root, submitted } = create();
    setText(root, '#sf-person', 'Ali Raza');
    setSelect(root, '#sf-result-1', 1);
    fixture.detectChanges();

    submit(root, fixture);

    expect(submitted.mock.calls[0][0].checks[0].affectedLines).toEqual([]);
    expect(submitted.mock.calls[0][0].checks[0].resultId).toBe(2);
  });

  it('shows recorded results in words, with what a failure means (AC-32)', () => {
    const { fixture } = create(
      inspection({
        safetyChecks: [
          { safetyTypeId: 1, resultId: 2, reason: null, affectedLines: [{ sizeId: 2, colourId: 10 }] },
          { safetyTypeId: 2, resultId: 3, reason: 'no drawstrings', affectedLines: [] },
        ],
      }),
    );

    const text = textOf(fixture);

    expect(text).toMatch(/Choking hazard on small trims:\s*Fail/);
    expect(text).toContain('Applies to Size 2 / Colour 10');
    expect(text).toContain('can only be rejected or sent back for rework');
    expect(text).toMatch(/Drawstring safety on necklines:\s*Not applicable/);
    expect(text).toContain('no drawstrings');
  });

  it('lets recorded results be corrected until a decision is recorded', () => {
    const { fixture, root } = create(inspection({ safetyChecks: bothSafetyPassed() }));

    click(root, 'button.button--small');
    fixture.detectChanges();

    expect(root.querySelector('#sf-result-1')).not.toBeNull();
    expect((root.querySelector('#sf-result-1') as HTMLSelectElement).value).toBe('1');
    expect(buttonByText(root, 'Cancel')).toBeTruthy();
  });

  it('freezes the results once a decision exists, with no way to change them (AC-31)', () => {
    const { fixture, root } = create(inspection({ safetyChecks: bothSafetyPassed() }), false);

    expect(textOf(fixture)).toMatch(/Choking hazard on small trims:\s*Pass/);
    expect(root.querySelector('button')).toBeNull();
    expect(root.querySelector('form')).toBeNull();
  });
});
