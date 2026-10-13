import { bothSafetyPassed, inspection, qcBatch, subBatch } from '@app/testing/quality-fixtures';
import { hasBothSafetyChecks, inspectionOfPart, nextStep, outstandingQty, previewReconciliation, splitAddsUp, splitBalance, todayInPakistan } from './quality.util';

describe('previewReconciliation (spec AC-7)', () => {
  const line = (claimedQty: number, countedQty: number) => ({ claimedQty, countedQty });

  it.each([
    ['exact', [line(100, 100), line(50, 50)], 'Matched', 0],
    ['one line short', [line(100, 90), line(50, 50)], 'Short', -10],
    ['one line over', [line(100, 100), line(50, 55)], 'Over', 5],
    ['mixed, fewer in total', [line(100, 80), line(50, 55)], 'Short', -15],
    ['mixed, more in total', [line(100, 95), line(50, 70)], 'Over', 15],
    ['mixed, equal totals is Short', [line(100, 90), line(50, 60)], 'Short', 0],
    ['nothing counted', [line(100, 0), line(50, 0)], 'Short', -150],
  ] as const)('%s', (_name, lines, code, diff) => {
    const result = previewReconciliation(lines);

    expect(result.code).toBe(code);
    expect(result.diffTotal).toBe(diff);
  });
});

describe('splitBalance (spec AC-22)', () => {
  const lines = [
    { sizeId: 1, colourId: 10, qty: 90 },
    { sizeId: 2, colourId: 10, qty: 50 },
  ];

  it('says what is still to place and when it adds up', () => {
    const partial = splitBalance(lines, [[{ sizeId: 1, colourId: 10, qty: 60 }], [{ sizeId: 2, colourId: 10, qty: 50 }]]);

    expect(partial.map((l) => l.remaining)).toEqual([30, 0]);
    expect(splitAddsUp(partial)).toBe(false);

    const complete = splitBalance(lines, [[{ sizeId: 1, colourId: 10, qty: 60 }], [{ sizeId: 1, colourId: 10, qty: 30 }, { sizeId: 2, colourId: 10, qty: 50 }]]);

    expect(splitAddsUp(complete)).toBe(true);
  });

  it('shows too many as a negative balance, so one unit over is not hidden', () => {
    const over = splitBalance(lines, [[{ sizeId: 1, colourId: 10, qty: 91 }, { sizeId: 2, colourId: 10, qty: 50 }]]);

    expect(over[0].remaining).toBe(-1);
    expect(splitAddsUp(over)).toBe(false);
  });
});

describe('batch helpers', () => {
  it('knows when both safety checks are recorded (AC-31)', () => {
    expect(hasBothSafetyChecks(null)).toBe(false);
    expect(hasBothSafetyChecks(inspection())).toBe(false);
    expect(hasBothSafetyChecks(inspection({ safetyChecks: bothSafetyPassed().slice(0, 1) }))).toBe(false);
    expect(hasBothSafetyChecks(inspection({ safetyChecks: bothSafetyPassed() }))).toBe(true);
  });

  it('says what the batch needs next, in order', () => {
    expect(nextStep(qcBatch())).toContain('Inspect the batch');
    expect(nextStep(qcBatch({ inspections: [inspection()] }))).toContain('child-safety checks');
    expect(nextStep(qcBatch({ inspections: [inspection({ safetyChecks: bothSafetyPassed() })] }))).toContain('outcome');
    expect(nextStep(qcBatch({ statusId: 3, subBatches: [subBatch()] }))).toBe('Every part has been decided.');
    expect(nextStep(qcBatch({ statusId: 4, subBatches: [subBatch({ statusId: 4 })] }))).toContain('still with the vendor');
  });

  it('finds the inspection of a returned part and its outstanding pieces (AC-26, AC-27)', () => {
    const part = subBatch({ id: 62, outcomeId: 4, lines: [{ sizeId: 2, colourId: 10, qty: 50, returnedQty: 20, outstandingQty: 30 }] });
    const batch = qcBatch({ inspections: [inspection(), inspection({ id: 52, subBatchId: 62, roundNo: 2 })], subBatches: [part] });

    expect(inspectionOfPart(batch, part)?.id).toBe(52);
    expect(outstandingQty(part)).toBe(30);
  });

  it('dates a record in Pakistan time (UTC+5)', () => {
    expect(todayInPakistan(new Date('2026-09-29T20:00:00Z'))).toBe('2026-09-30');
    expect(todayInPakistan(new Date('2026-09-29T10:00:00Z'))).toBe('2026-09-29');
  });
});
