import { TestBed } from '@angular/core/testing';
import { alertRow } from '@app/testing/warehouse-fixtures';
import { textOf } from '@app/testing/production-fixtures';
import { AlertRowDto } from '../../models';
import { AlertTableComponent } from './alert-table.component';
import { provideAllowAllPermissions } from '@app/testing/provide-allow-all-permissions';

describe('AlertTableComponent (spec ALR-1, ALR-2, ALR-5, ALR-8)', () => {
  beforeEach(() => TestBed.configureTestingModule({ providers: [provideAllowAllPermissions()] }));
  function create(alerts: AlertRowDto[], loading = false, acknowledgingId: number | null = null) {
    const fixture = TestBed.createComponent(AlertTableComponent);
    fixture.componentRef.setInput('alerts', alerts);
    fixture.componentRef.setInput('loading', loading);
    fixture.componentRef.setInput('acknowledgingId', acknowledgingId);
    const acknowledge = vi.fn();
    fixture.componentInstance.acknowledge.subscribe(acknowledge);
    fixture.detectChanges();
    return { fixture, root: fixture.nativeElement as HTMLElement, acknowledge };
  }

  it('says when nothing is open, and shows loading', () => {
    expect(textOf(create([]).fixture)).toContain('No open alerts.');
    expect(textOf(create([], true).fixture)).toContain('Loading…');
  });

  it('names the level in words (not colour alone) with the available quantity and the threshold (ALR-1, ALR-2)', () => {
    const text = textOf(create([alertRow(), alertRow({ id: 91, levelCode: 'OUT', available: 0 })]).fixture);

    expect(text).toContain('Low stock');
    expect(text).toContain('Stock-out');
    expect(text).toContain('Threshold');
  });

  it('shows who has seen an alert, and offers Acknowledge only for one nobody has seen (ALR-5)', () => {
    const { root, acknowledge, fixture } = create([alertRow(), alertRow({ id: 91, acknowledged: true, acknowledgedBy: 'Bilal Ahmed' })]);

    const buttons = Array.from(root.querySelectorAll('button')) as HTMLButtonElement[];
    expect(buttons[0].disabled).toBe(false);
    expect(buttons[1].disabled).toBe(true);
    expect(buttons[1].textContent).toContain('Acknowledged');
    expect(textOf(fixture)).toContain('Bilal Ahmed');
    expect(textOf(fixture)).toContain('Not yet');
    buttons[0].click();
    expect(acknowledge).toHaveBeenCalledWith(expect.objectContaining({ id: 90 }));
  });

  it('only the alert being acknowledged is disabled', () => {
    const { root } = create([alertRow({ id: 1 }), alertRow({ id: 2 })], false, 2);

    const buttons = Array.from(root.querySelectorAll('button')) as HTMLButtonElement[];
    expect(buttons[0].disabled).toBe(false);
    expect(buttons[1].disabled).toBe(true);
  });

  it('is a real table with column headers and a caption (ALR-8)', () => {
    const { root } = create([alertRow()]);

    expect(root.querySelector('caption')?.textContent).toContain('Open stock alerts');
    expect(Array.from(root.querySelectorAll('th')).every((th) => th.getAttribute('scope') === 'col')).toBe(true);
  });
});
