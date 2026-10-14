import { TestBed } from '@angular/core/testing';
import { textOf } from '@app/testing/production-fixtures';
import { VendorScorecardDto } from '../../models';
import { ScorecardPanelComponent } from './scorecard-panel.component';

describe('ScorecardPanelComponent (spec AC-35..AC-37, AC-39)', () => {
  const scorecard = (overrides: Partial<VendorScorecardDto> = {}): VendorScorecardDto => ({
    vendorId: 1,
    windowDays: 90,
    deliveriesCounted: 4,
    deliveriesOnTime: 2,
    deliveriesOnQuantity: 3,
    deliveriesExcludedRompDelay: 0,
    inspectedUnits: 80,
    defectiveUnits: 4,
    onTimePct: 50,
    onQuantityPct: 75,
    defectRatePct: 5,
    onTimePctRolling: 100,
    onQuantityPctRolling: 100,
    defectRatePctRolling: null,
    ...overrides,
  });

  function create(card: VendorScorecardDto | null, loading = false, error = null as { status: number; message: string; fieldErrors: Record<string, string[]> } | null) {
    const fixture = TestBed.createComponent(ScorecardPanelComponent);
    fixture.componentRef.setInput('vendorName', 'IT Garments');
    fixture.componentRef.setInput('scorecard', card);
    fixture.componentRef.setInput('loading', loading);
    fixture.componentRef.setInput('error', error);
    const closed = vi.fn();
    fixture.componentInstance.closed.subscribe(closed);
    fixture.detectChanges();
    return { fixture, root: fixture.nativeElement as HTMLElement, closed };
  }

  it('shows the three measures all time and for the window, with the counts they rest on (AC-35)', () => {
    const { fixture } = create(scorecard());

    const text = textOf(fixture);

    expect(text).toContain('IT Garments: scorecard');
    expect(text).toContain('Last 90 days');
    expect(text).toContain('50%');
    expect(text).toContain('75%');
    expect(text).toContain('2 of 4 deliveries');
    expect(text).toContain('3 of 4 deliveries');
    expect(text).toContain('4 defective of 80 pieces inspected');
  });

  it('says "No data yet" for a measure with nothing behind it, never 0% or 100% (AC-37)', () => {
    const { fixture } = create(scorecard({ onTimePct: null, onQuantityPct: null, defectRatePct: null, onTimePctRolling: null, onQuantityPctRolling: null, defectRatePctRolling: null, deliveriesCounted: 0, inspectedUnits: 0, defectiveUnits: 0, deliveriesOnTime: 0, deliveriesOnQuantity: 0 }));

    const text = textOf(fixture);

    expect(text.match(/No data yet/g)).toHaveLength(6);
    expect(text).not.toContain('0%');
    expect(text).not.toContain('100%');
  });

  it('shows a real 0% as 0%, not as missing', () => {
    const { fixture } = create(scorecard({ defectRatePct: 0, defectiveUnits: 0 }));

    expect(textOf(fixture)).toContain('0%');
  });

  it('shows the deliveries left out because Romp sent the fabric late, as a count (AC-36)', () => {
    const { fixture } = create(scorecard({ deliveriesCounted: 4, deliveriesExcludedRompDelay: 1, deliveriesOnTime: 3, onTimePct: 100 }));

    const text = textOf(fixture);

    expect(text).toContain('3 of 3 deliveries');
    expect(text).toContain('1 left out because Romp sent the fabric late');
  });

  it('shows loading and errors in words, and can be closed', () => {
    expect(textOf(create(null, true).fixture)).toContain('Loading');
    expect(textOf(create(null, false, { status: 404, message: 'Vendor 9 was not found.', fieldErrors: {} }).fixture)).toContain('Vendor 9 was not found');

    const { root, closed } = create(scorecard());
    (root.querySelector('button') as HTMLButtonElement).click();
    expect(closed).toHaveBeenCalled();
  });
});
