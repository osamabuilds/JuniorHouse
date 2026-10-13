import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { runSummary, textOf } from '@app/testing/production-fixtures';
import { RunTableComponent } from './run-table.component';

describe('RunTableComponent (spec AC-16, AC-33)', () => {
  function create(runs: ReturnType<typeof runSummary>[], loading = false) {
    TestBed.configureTestingModule({ providers: [provideRouter([])] });
    const fixture = TestBed.createComponent(RunTableComponent);
    fixture.componentRef.setInput('runs', runs);
    fixture.componentRef.setInput('loading', loading);
    fixture.detectChanges();
    return fixture;
  }

  it('says what to expect when there are no runs yet', () => {
    expect(textOf(create([]))).toContain('No production runs yet');
  });

  it('shows a loading message while the list loads', () => {
    expect(textOf(create([], true))).toContain('Loading');
  });

  it('links each row to its run and shows the sample, shipped pieces and dates in words', () => {
    const fixture = create([
      runSummary({ id: 7, poNo: 'PO-2026-00007', hasApprovedSample: true, isCuttingStarted: true, expectedCompletionDate: '2026-10-20', shippedQty: 80 }),
      runSummary({ id: 8, poNo: 'PO-2026-00008' }),
    ]);
    const root = fixture.nativeElement as HTMLElement;

    const links = Array.from(root.querySelectorAll('tbody a')).map((a) => a.getAttribute('href'));
    expect(links).toEqual(['/production/7', '/production/8']);

    const text = textOf(fixture);
    expect(text).toContain('Approved');
    expect(text).toContain('Not approved yet');
    expect(text).toContain('20 Oct 2026');
    expect(text).toContain('80 pieces');
    expect(text).toContain('Cutting not started');
  });

  it('writes the warnings out as text, not colour alone', () => {
    const fixture = create([runSummary({ isAtRisk: true, hasShortDelivery: true, isCuttingBeforeFabricDispatch: true })]);

    const text = textOf(fixture);

    expect(text).toContain('At risk of being late');
    expect(text).toContain('Delivered short');
    expect(text).toContain('Cutting started before Romp sent the fabric');
  });

  it('marks a closed or cancelled run as muted text as well as colour', () => {
    const fixture = create([runSummary({ statusId: 2 })]);

    expect(textOf(fixture)).toContain('Cancelled');
  });

  it('has table headings with scope and a caption (semantic table)', () => {
    const root = create([runSummary()]).nativeElement as HTMLElement;

    expect(root.querySelector('caption')).not.toBeNull();
    expect(root.querySelectorAll('th[scope="col"]').length).toBeGreaterThanOrEqual(5);
  });
});
