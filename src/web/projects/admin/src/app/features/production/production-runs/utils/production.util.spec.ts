import { runFlags, milestoneChoices, todayInPakistan } from './production.util';

describe('todayInPakistan', () => {
  it('is the Pakistan-time day whatever the browser zone: late evening UTC is already tomorrow there', () => {
    expect(todayInPakistan(new Date('2026-09-29T10:00:00Z'))).toBe('2026-09-29');
    expect(todayInPakistan(new Date('2026-09-29T19:30:00Z'))).toBe('2026-09-30');
    expect(todayInPakistan(new Date('2026-12-31T20:00:00Z'))).toBe('2027-01-01');
  });
});

describe('milestoneChoices (spec D10, AC-4, AC-9)', () => {
  const ids = (choices: ReturnType<typeof milestoneChoices>) => choices.map((choice) => choice.id);

  it('offers fabric booked (only) when the vendor supplies the fabric', () => {
    const choices = milestoneChoices({ fabricResponsibilityId: 1, hasApprovedSample: false, cuttingStartedDate: null });

    expect(ids(choices)).toEqual([1, 6]);
  });

  it('offers fabric dispatch (only) when Romp supplies the fabric', () => {
    const choices = milestoneChoices({ fabricResponsibilityId: 2, hasApprovedSample: false, cuttingStartedDate: null });

    expect(ids(choices)).toEqual([2, 6]);
  });

  it('offers neither fabric step when the PO does not say who supplies the fabric', () => {
    expect(ids(milestoneChoices({ fabricResponsibilityId: null, hasApprovedSample: true, cuttingStartedDate: null }))).toEqual([6]);
  });

  it('lists bulk cutting but blocks it, with the reason, until a sample is approved', () => {
    const cutting = milestoneChoices({ fabricResponsibilityId: 1, hasApprovedSample: false, cuttingStartedDate: null }).find((choice) => choice.id === 6);
    const allowed = milestoneChoices({ fabricResponsibilityId: 1, hasApprovedSample: true, cuttingStartedDate: null }).find((choice) => choice.id === 6);

    expect(cutting?.disabledReason).toContain('approved pre-production sample');
    expect(allowed?.disabledReason).toBeNull();
  });

  it('replaces bulk cutting with finished-and-ready once cutting has started', () => {
    const choices = milestoneChoices({ fabricResponsibilityId: 1, hasApprovedSample: true, cuttingStartedDate: '2026-09-10' });

    expect(ids(choices)).toEqual([1, 7]);
  });
});

describe('runFlags (spec AC-33: warnings as words)', () => {
  const none = { isAtRisk: false, isCuttingBeforeFabricDispatch: false, isResampleRecommended: false, isCuttingWithoutResample: false };

  it('has nothing to say about a healthy run', () => {
    expect(runFlags(none)).toEqual([]);
  });

  it('writes each warning out in plain words', () => {
    const labels = runFlags({
      ...none,
      isAtRisk: true,
      isCuttingBeforeFabricDispatch: true,
      isResampleRecommended: true,
      hasOverDelivery: true,
      hasShortDelivery: true,
      hasLateDelivery: true,
    }).map((flag) => flag.label);

    expect(labels).toEqual([
      'At risk of being late',
      'Cutting started before Romp sent the fabric',
      'A new sample is strongly recommended',
      'Delivered more than allowed',
      'Delivered short',
      'Delivered late',
    ]);
  });

  it('shows the stronger "cutting without a new sample" warning instead of the recommendation once cutting went ahead', () => {
    const flags = runFlags({ ...none, isResampleRecommended: true, isCuttingWithoutResample: true });

    expect(flags.map((flag) => flag.code)).toEqual(['cut-without-resample']);
    expect(flags[0].tone).toBe('danger');
  });
});
