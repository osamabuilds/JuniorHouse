import { TestBed } from '@angular/core/testing';
import { AuditEntryDto } from '../../models';
import { AuditListComponent } from './audit-list.component';

const ENTRY: AuditEntryDto = {
  id: 7,
  occurredAt: '2026-10-26T09:00:00Z',
  module: 'CTLG',
  entityType: 'STYL_MAIN',
  entityKey: '42',
  operation: 'UPDATE',
  actor: 'amna.khan',
  correlationId: 'abc',
  columns: ['NAME', 'SLUG'],
};

describe('AuditListComponent (audit-trail AC-9)', () => {
  function create(entries: readonly AuditEntryDto[], loading = false) {
    const fixture = TestBed.createComponent(AuditListComponent);
    fixture.componentRef.setInput('entries', entries);
    fixture.componentRef.setInput('loading', loading);
    const opened = vi.fn<(entry: AuditEntryDto) => void>();
    fixture.componentInstance.selected.subscribe(opened);
    fixture.detectChanges();
    return { root: fixture.nativeElement as HTMLElement, opened };
  }

  it('is a table with column headings, a caption and one row per entry', () => {
    const { root } = create([ENTRY, { ...ENTRY, id: 8, operation: 'INSERT' }]);

    expect(root.querySelectorAll('table')).toHaveLength(1);
    expect(root.querySelector('caption')?.textContent).toContain('Audit entries');
    expect(Array.from(root.querySelectorAll('thead th[scope="col"]')).map((th) => th.textContent?.trim())).toEqual(['When', 'Who', 'What happened', 'Record', 'Actions']);
    expect(root.querySelectorAll('tbody tr')).toHaveLength(2);
    expect(root.querySelector('tbody')?.textContent).toContain('amna.khan');
    expect(root.querySelector('tbody')?.textContent).toContain('STYL_MAIN');
    expect(root.querySelector('time')?.getAttribute('datetime')).toBe('2026-10-26T09:00:00Z');
  });

  it('opens an entry with a named button', () => {
    const { root, opened } = create([ENTRY]);

    root.querySelector<HTMLButtonElement>('button[aria-label="Open entry 7"]')?.click();

    expect(opened).toHaveBeenCalledWith(ENTRY);
  });

  it('says so when nothing matches, and while loading', () => {
    expect(create([]).root.textContent).toContain('No entries match.');
    expect(create([], true).root.textContent).toContain('Loading');
  });
});
