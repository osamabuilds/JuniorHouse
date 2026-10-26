import { TestBed } from '@angular/core/testing';
import { printJob } from '@app/testing/labelling-fixtures';
import { textOf } from '@app/testing/production-fixtures';
import { PrintJobDto } from '../../models';
import { JobTableComponent } from './job-table.component';
import { provideAllowAllPermissions } from '@app/testing/provide-allow-all-permissions';

describe('JobTableComponent (spec PJ-2, PJ-3, PJ-6, PJ-13)', () => {
  beforeEach(() => TestBed.configureTestingModule({ providers: [provideAllowAllPermissions()] }));
  function create(jobs: PrintJobDto[], loading = false, busyId: number | null = null) {
    const fixture = TestBed.createComponent(JobTableComponent);
    fixture.componentRef.setInput('jobs', jobs);
    fixture.componentRef.setInput('loading', loading);
    fixture.componentRef.setInput('busyId', busyId);
    const retry = vi.fn();
    const cancel = vi.fn();
    fixture.componentInstance.retry.subscribe(retry);
    fixture.componentInstance.cancel.subscribe(cancel);
    fixture.detectChanges();
    return { fixture, root: fixture.nativeElement as HTMLElement, retry, cancel };
  }

  const buttons = (root: HTMLElement) => Array.from(root.querySelectorAll('button')).map((b) => b.textContent?.trim());

  it('says so when there are no jobs', () => {
    expect(textOf(create([]).fixture)).toContain('No print jobs yet.');
  });

  it('shows the job, SKU, barcode, tag count, printer and who asked', () => {
    const text = textOf(create([printJob()]).fixture);

    expect(text).toContain('PJ-2026-00003');
    expect(text).toContain('First printing');
    expect(text).toContain('ROMP-STY-DRESS-RED-2-3Y');
    expect(text).toContain('0200000000019');
    expect(text).toContain('FAKE-PDF');
    expect(text).toContain('Asha');
    expect(text).toContain('Waiting for the printer');
  });

  it('a queued job that failed once shows the attempt and the reason, and can only be cancelled (PJ-2)', () => {
    const { fixture, root } = create([printJob({ attemptCount: 1, lastError: 'Paper jam (simulated).' })]);

    expect(textOf(fixture)).toContain('Attempt 1 failed; trying again');
    expect(textOf(fixture)).toContain('Paper jam (simulated).');
    expect(buttons(root)).toEqual(['Cancel']);
  });

  it('a failed job can be retried or cancelled, and the buttons emit the job (PJ-3)', () => {
    const job = printJob({ statusCode: 'Failed', attemptCount: 3, lastError: 'Paper jam (simulated).' });
    const { root, retry, cancel } = create([job]);

    expect(buttons(root)).toEqual(['Retry', 'Cancel']);
    (root.querySelectorAll('button')[0] as HTMLButtonElement).click();
    (root.querySelectorAll('button')[1] as HTMLButtonElement).click();
    expect(retry).toHaveBeenCalledWith(job);
    expect(cancel).toHaveBeenCalledWith(job);
  });

  it('printed and cancelled jobs have no actions, and the busy job\'s buttons are disabled', () => {
    expect(buttons(create([printJob({ statusCode: 'Printed' })]).root)).toEqual([]);
    expect(buttons(create([printJob({ statusCode: 'Cancelled' })]).root)).toEqual([]);

    const busy = create([printJob({ id: 9, statusCode: 'Failed' })], false, 9);
    expect(Array.from(busy.root.querySelectorAll('button')).every((b) => b.disabled)).toBe(true);
  });

  it('a reprint shows why it was made and any note (PJ-6)', () => {
    const text = textOf(create([printJob({ kindCode: 'Reprint', reprintReasonCode: 'Damaged', note: 'ripped in the bin', workItemId: null })]).fixture);

    expect(text).toContain('Reprint');
    expect(text).toContain('Reason: Damaged (ripped in the bin)');
  });

  it('is a real table with a caption and column headers (NFR-19)', () => {
    const { root } = create([printJob()]);

    expect(root.querySelector('table caption')?.textContent).toContain('Print jobs');
    expect(Array.from(root.querySelectorAll('th')).every((th) => th.getAttribute('scope') === 'col')).toBe(true);
  });
});
