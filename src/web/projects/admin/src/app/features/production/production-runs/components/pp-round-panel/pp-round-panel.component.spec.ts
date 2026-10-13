import { TestBed } from '@angular/core/testing';
import { buttonByText, round, setText, textOf } from '@app/testing/production-fixtures';
import { DecideSampleValue, PpSampleRoundDto, SubmitSampleValue } from '../../models';
import { PpRoundPanelComponent } from './pp-round-panel.component';

describe('PpRoundPanelComponent (spec AC-10..AC-13, D6, D7)', () => {
  function create(rounds: PpSampleRoundDto[], options: { canSubmit?: boolean; submitFormOpen?: boolean; defaultPerson?: string; runOpen?: boolean } = {}) {
    const fixture = TestBed.createComponent(PpRoundPanelComponent);
    fixture.componentRef.setInput('rounds', rounds);
    fixture.componentRef.setInput('canSubmit', options.canSubmit ?? true);
    fixture.componentRef.setInput('runOpen', options.runOpen ?? true);
    fixture.componentRef.setInput('submitFormOpen', options.submitFormOpen ?? false);
    fixture.componentRef.setInput('minDate', '2026-09-01');
    fixture.componentRef.setInput('defaultPerson', options.defaultPerson ?? '');
    fixture.componentRef.setInput('fileUrlFor', (roundNo: number, fileId: number) => `/files/${roundNo}/${fileId}`);
    const decided = vi.fn();
    const submitted = vi.fn();
    const fileSelected = vi.fn();
    const fileRemoved = vi.fn();
    fixture.componentInstance.decided.subscribe(decided);
    fixture.componentInstance.submitted.subscribe(submitted);
    fixture.componentInstance.fileSelected.subscribe(fileSelected);
    fixture.componentInstance.fileRemoved.subscribe(fileRemoved);
    fixture.detectChanges();
    return { fixture, root: fixture.nativeElement as HTMLElement, decided, submitted, fileSelected, fileRemoved };
  }

  it('explains that cutting waits for an approved sample when there are no rounds', () => {
    const { fixture } = create([]);

    expect(textOf(fixture)).toContain('Bulk cutting cannot be recorded until a pre-production sample is approved');
  });

  it('shows every round with its number, decision and reason, so the history is readable (AC-11)', () => {
    const { fixture } = create([
      round({ roundNo: 1, decisionCode: 'Rejected', decidedAt: '2026-09-06T08:00:00Z', decidedBy: 'Osama Shafique', decisionReason: 'collar too wide' }),
      round({ id: 2, roundNo: 2, decisionCode: 'Approved', decidedAt: '2026-09-09T08:00:00Z', decidedBy: 'Osama Shafique' }),
    ]);

    const text = textOf(fixture);

    expect(text).toContain('Round 1');
    expect(text).toContain('Rejected');
    expect(text).toContain('Reason: collar too wide');
    expect(text).toContain('Round 2');
    expect(text).toContain('Approved');
  });

  it('offers the decision only on the round that is waiting', () => {
    const { root } = create([round({ roundNo: 1, decisionCode: 'Rejected' }), round({ id: 2, roundNo: 2 })]);

    expect(root.querySelectorAll('.decide').length).toBe(1);
    expect(root.querySelector('#round-file-2')).not.toBeNull();
    expect(root.querySelector('#round-file-1')).toBeNull();
  });

  it('approves with the name of the person, trimmed (AC-10, AC-35)', () => {
    const { fixture, root, decided } = create([round()]);
    setText(root, '#decide-person-1', '  Osama Shafique ');

    buttonByText(root, 'Approve sample').click();
    fixture.detectChanges();

    expect(decided).toHaveBeenCalledOnce();
    expect(decided.mock.calls[0][0] as DecideSampleValue).toEqual({ roundNo: 1, approve: true, reason: null, actingPerson: 'Osama Shafique' });
  });

  it('refuses to reject without a reason and says why (AC-11)', () => {
    const { fixture, root, decided } = create([round()], { defaultPerson: 'Osama Shafique' });

    buttonByText(root, 'Reject sample').click();
    fixture.detectChanges();

    expect(decided).not.toHaveBeenCalled();
    expect(textOf(fixture)).toContain('Say why the sample is rejected');
  });

  it('rejects with a reason', () => {
    const { fixture, root, decided } = create([round()], { defaultPerson: 'Osama Shafique' });
    setText(root, '#decide-reason-1', 'neck too tight');

    buttonByText(root, 'Reject sample').click();
    fixture.detectChanges();

    expect(decided.mock.calls[0][0] as DecideSampleValue).toEqual({ roundNo: 1, approve: false, reason: 'neck too tight', actingPerson: 'Osama Shafique' });
  });

  it('needs a name to decide', () => {
    const { fixture, root, decided } = create([round()]);

    buttonByText(root, 'Approve sample').click();
    fixture.detectChanges();

    expect(decided).not.toHaveBeenCalled();
    expect(textOf(fixture)).toContain('your name');
  });

  it('lists a round\'s files as download links, and lets a pending round\'s file be removed (D7)', () => {
    const { root, fileRemoved } = create([round({ files: [{ id: 9, fileName: 'front.png', contentType: 'image/png', fileSizeBytes: 2048, uploadedAt: '2026-09-05T08:00:00Z' }] })]);

    const link = root.querySelector('.round__files a') as HTMLAnchorElement;
    expect(link.getAttribute('href')).toBe('/files/1/9');
    expect(link.textContent).toContain('front.png');

    buttonByText(root, 'Remove').click();
    expect(fileRemoved).toHaveBeenCalledWith({ roundNo: 1, fileId: 9 });
  });

  it('emits the chosen file for the round', () => {
    const { root, fileSelected } = create([round()]);
    const input = root.querySelector('#round-file-1') as HTMLInputElement;
    const file = new File(['png'], 'front.png', { type: 'image/png' });
    Object.defineProperty(input, 'files', { value: [file] });

    input.dispatchEvent(new Event('change'));

    expect(fileSelected).toHaveBeenCalledWith({ roundNo: 1, file });
  });

  it('shows the file error from the API', () => {
    const { fixture } = create([round()]);
    fixture.componentRef.setInput('fileError', { status: 400, message: "This file type isn't allowed.", fieldErrors: {} });
    fixture.detectChanges();

    expect(textOf(fixture)).toContain("This file type isn't allowed.");
  });

  it('records a new sample: bounded date, needs vendor contact and name (AC-11)', () => {
    const { fixture, root, submitted } = create([], { submitFormOpen: true });

    (root.querySelector('form[aria-labelledby="sample-heading"]') as HTMLFormElement).dispatchEvent(new Event('submit'));
    expect(submitted).not.toHaveBeenCalled();

    setText(root, '#sample-reporter', 'Ali Raza');
    setText(root, '#sample-person', 'Osama Shafique');
    (root.querySelector('form[aria-labelledby="sample-heading"]') as HTMLFormElement).dispatchEvent(new Event('submit'));
    fixture.detectChanges();

    expect((root.querySelector('#sample-date') as HTMLInputElement).min).toBe('2026-09-01');
    const value = submitted.mock.calls[0][0] as SubmitSampleValue;
    expect(value).toMatchObject({ reportedBy: 'Ali Raza', actingPerson: 'Osama Shafique', note: null });
  });

  it('offers to record another sample after a rejection, but not while one is waiting or after approval (AC-13)', () => {
    expect(textOf(create([round({ decisionCode: 'Rejected' })], { canSubmit: true }).fixture)).toContain('Record another sample');
    expect(textOf(create([round()], { canSubmit: false }).fixture)).not.toContain('Record another sample');
    expect(textOf(create([round({ decisionCode: 'Approved' })], { canSubmit: false }).fixture)).not.toContain('Record another sample');
  });

  it('offers nothing on a cancelled or closed run', () => {
    const { fixture, root } = create([round()], { runOpen: false, canSubmit: false });

    expect(root.querySelector('.decide')).toBeNull();
    expect(textOf(fixture)).not.toContain('Record a sample');
  });
});
