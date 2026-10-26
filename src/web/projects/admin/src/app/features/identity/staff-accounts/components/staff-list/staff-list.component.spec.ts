import { TestBed } from '@angular/core/testing';
import { staffMember } from '@app/testing/identity-fixtures';
import { StaffDto } from '../../models';
import { StaffListComponent } from './staff-list.component';

describe('StaffListComponent (spec AC-3, AC-8)', () => {
  function create(staff: readonly StaffDto[], loading = false) {
    const fixture = TestBed.createComponent(StaffListComponent);
    fixture.componentRef.setInput('staff', staff);
    fixture.componentRef.setInput('loading', loading);
    const selected = vi.fn<(person: StaffDto) => void>();
    fixture.componentInstance.selected.subscribe(selected);
    fixture.detectChanges();
    return { root: fixture.nativeElement as HTMLElement, selected };
  }

  it('shows one row per account with roles and status, in a table with column headers', () => {
    const { root } = create([
      staffMember(),
      staffMember({ id: 12, userName: 'bilal.s', displayName: 'Bilal S', isActive: false, mustChangePassword: true, roles: [] }),
    ]);

    expect(root.querySelectorAll('tbody tr')).toHaveLength(2);
    expect(Array.from(root.querySelectorAll('thead th')).every((th) => th.getAttribute('scope') === 'col')).toBe(true);
    const text = (root.textContent ?? '').replace(/\s+/g, ' ');
    expect(text).toContain('Catalogue');
    expect(text).toContain('Deactivated');
    expect(text).toContain('Must change password');
    expect(text).toContain('None');
  });

  it('says so when there are no accounts, and while loading', () => {
    expect(create([]).root.textContent).toContain('No staff accounts found');
    expect(create([], true).root.textContent).toContain('Loading');
  });

  it('opens the account whose button was pressed, and names the button after the person', () => {
    const { root, selected } = create([staffMember()]);
    const button = root.querySelector('button') as HTMLButtonElement;

    expect(button.getAttribute('aria-label')).toBe('Open amna.khan');
    button.click();

    expect(selected).toHaveBeenCalledWith(expect.objectContaining({ id: 11 }));
  });

  it('never shows anything that looks like a password or hash (AC-3)', () => {
    const { root } = create([staffMember({ hasTemporaryPassword: true, temporaryPasswordExpiresAt: '2026-10-28T09:00:00Z' })]);

    expect((root.textContent ?? '').toLowerCase()).not.toMatch(/argon|hash/);
  });
});
