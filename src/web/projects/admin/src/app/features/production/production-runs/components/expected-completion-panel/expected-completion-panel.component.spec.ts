import { TestBed } from '@angular/core/testing';
import { buttonByText, setText, textOf } from '@app/testing/production-fixtures';
import { ExpectedCompletionValue } from '../../models';
import { ExpectedCompletionPanelComponent } from './expected-completion-panel.component';

describe('ExpectedCompletionPanelComponent (spec AC-15, AC-16)', () => {
  function create(inputs: Record<string, unknown> = {}) {
    const fixture = TestBed.createComponent(ExpectedCompletionPanelComponent);
    for (const [key, value] of Object.entries({ cuttingStarted: true, ...inputs })) {
      fixture.componentRef.setInput(key, value);
    }
    const submitted = vi.fn();
    const opened = vi.fn();
    fixture.componentInstance.submitted.subscribe(submitted);
    fixture.componentInstance.opened.subscribe(opened);
    fixture.detectChanges();
    return { fixture, root: fixture.nativeElement as HTMLElement, submitted, opened };
  }

  it('says the date is set once cutting starts, and offers no button before that', () => {
    const { fixture, root } = create({ cuttingStarted: false });

    expect(textOf(fixture)).toContain('Set once bulk cutting has started');
    expect(root.querySelector('button')).toBeNull();
  });

  it('shows the current date and the latest acceptable delivery', () => {
    const { fixture } = create({ expectedDate: '2026-10-20', latestAcceptableDate: '2026-10-25' });

    const text = textOf(fixture);

    expect(text).toContain('20 Oct 2026');
    expect(text).toContain('25 Oct 2026');
  });

  it('warns in words when the run is at risk of being late (AC-16)', () => {
    const { fixture } = create({ expectedDate: '2026-10-24', atRisk: true });

    expect(textOf(fixture)).toContain('At risk of being late');
  });

  it('shows every earlier estimate with who changed it and why (AC-15)', () => {
    const { fixture } = create({
      expectedDate: '2026-10-20',
      history: [
        { oldDate: null, newDate: '2026-10-10', reason: 'initial estimate', actingPerson: 'Osama Shafique', changedAt: '2026-09-11T08:00:00Z' },
        { oldDate: '2026-10-10', newDate: '2026-10-20', reason: 'vendor short of trims', actingPerson: 'Osama Shafique', changedAt: '2026-09-20T08:00:00Z' },
      ],
    });

    const text = textOf(fixture);

    expect(text).toContain('First estimate: 10 Oct 2026');
    expect(text).toContain('10 Oct 2026 → 20 Oct 2026');
    expect(text).toContain('vendor short of trims');
  });

  it('asks the parent to open the form', () => {
    const { root, opened } = create();

    buttonByText(root, 'Set the date').click();

    expect(opened).toHaveBeenCalledOnce();
  });

  it('needs a date and a name before it emits', () => {
    const { fixture, root, submitted } = create({ formOpen: true });
    const form = root.querySelector('form') as HTMLFormElement;

    form.dispatchEvent(new Event('submit'));
    expect(submitted).not.toHaveBeenCalled();

    setText(root, '#expected-date', '2026-10-20');
    setText(root, '#expected-person', ' Osama Shafique ');
    setText(root, '#expected-reason', 'vendor short of trims');
    form.dispatchEvent(new Event('submit'));
    fixture.detectChanges();

    expect(submitted.mock.calls[0][0] as ExpectedCompletionValue).toEqual({ date: '2026-10-20', reason: 'vendor short of trims', actingPerson: 'Osama Shafique' });
  });

  it('offers nothing on a closed run', () => {
    const { root } = create({ runOpen: false });

    expect(root.querySelector('button')).toBeNull();
  });
});
