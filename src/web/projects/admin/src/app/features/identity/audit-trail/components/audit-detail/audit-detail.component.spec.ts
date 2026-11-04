import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { AuditEntryDto } from '../../models';
import { AuditDetailComponent } from './audit-detail.component';

const ENTRY: AuditEntryDto = {
  id: 7,
  occurredAt: '2026-10-26T09:00:00Z',
  module: 'IDNT',
  entityType: 'USR_MAIN',
  entityKey: '42',
  operation: 'UPDATE',
  actor: 'osama',
  correlationId: null,
  columns: ['ACT_IND'],
};

describe('AuditDetailComponent (audit-trail AC-6, AC-9)', () => {
  function create(entry: AuditEntryDto) {
    TestBed.configureTestingModule({ providers: [provideRouter([])] });
    const fixture = TestBed.createComponent(AuditDetailComponent);
    fixture.componentRef.setInput('entry', entry);
    const closed = vi.fn();
    fixture.componentInstance.closed.subscribe(closed);
    fixture.detectChanges();
    return { root: fixture.nativeElement as HTMLElement, closed };
  }

  it('lists the changed column names and says no values are kept', () => {
    const { root } = create(ENTRY);

    expect(root.querySelectorAll('h2')).toHaveLength(1);
    expect(Array.from(root.querySelectorAll('li code')).map((c) => c.textContent)).toEqual(['ACT_IND']);
    expect(root.textContent).toContain('never their values');
  });

  it('links to the record screen when one exists, and not for a deletion or an unknown table', () => {
    expect(create(ENTRY).root.querySelector('a')?.getAttribute('href')).toBe('/staff');
  });

  it('has no link for a deleted record', () => {
    expect(create({ ...ENTRY, operation: 'DELETE' }).root.querySelector('a')).toBeNull();
  });

  it('has no link for a table without a screen', () => {
    expect(create({ ...ENTRY, entityType: 'OUTB_X' }).root.querySelector('a')).toBeNull();
  });

  it('explains an entry with no column names', () => {
    expect(create({ ...ENTRY, columns: [] }).root.textContent).toContain('None named');
  });

  it('goes back to the list', () => {
    const { root, closed } = create(ENTRY);

    root.querySelector<HTMLButtonElement>('button')?.click();

    expect(closed).toHaveBeenCalled();
  });
});
