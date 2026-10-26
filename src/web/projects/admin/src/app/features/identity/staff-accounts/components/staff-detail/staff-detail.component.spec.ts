import { TestBed } from '@angular/core/testing';
import { buttonNamed, controlsWithoutLabels, setChecked } from '@app/testing/dom-helpers';
import { roleOption, staffMember } from '@app/testing/identity-fixtures';
import { StaffDetailComponent } from './staff-detail.component';

const ROLES = [
  roleOption({ id: 1, code: 'OWNER', name: 'Owner', hasAllPermissions: true }),
  roleOption({ id: 2, code: 'CATALOGUE', name: 'Catalogue' }),
  roleOption({ id: 3, code: 'ORDER_OPS', name: 'Order-ops' }),
];

describe('StaffDetailComponent roles (roles AC-6)', () => {
  function create() {
    const fixture = TestBed.createComponent(StaffDetailComponent);
    fixture.componentRef.setInput('staff', staffMember({ roles: [{ id: 2, code: 'CATALOGUE', name: 'Catalogue' }] }));
    fixture.componentRef.setInput('roles', ROLES);
    const saved = vi.fn<(ids: readonly number[]) => void>();
    fixture.componentInstance.rolesSaved.subscribe(saved);
    fixture.detectChanges();
    return { fixture, root: fixture.nativeElement as HTMLElement, saved };
  }

  const checkboxes = (root: HTMLElement) => Array.from(root.querySelectorAll<HTMLInputElement>('fieldset input[type="checkbox"]'));

  it('offers every role as a checkbox in a labelled group, ticked for the roles the person holds', () => {
    const { root } = create();

    expect(root.querySelector('fieldset legend')?.textContent).toBe('Roles');
    expect(controlsWithoutLabels(root)).toEqual([]);
    expect(checkboxes(root).map((c) => c.checked)).toEqual([false, true, false]);
  });

  it('saves the chosen set, as ids', () => {
    const { fixture, root, saved } = create();
    setChecked(root, 'fieldset label:nth-of-type(3) input', true);
    fixture.detectChanges();

    buttonNamed(root, 'Save roles').click();

    expect(saved).toHaveBeenCalledWith([2, 3]);
  });

  it('can leave a person with no role at all', () => {
    const { fixture, root, saved } = create();
    setChecked(root, 'fieldset label:nth-of-type(2) input', false);
    fixture.detectChanges();

    buttonNamed(root, 'Save roles').click();

    expect(saved).toHaveBeenCalledWith([]);
  });

  it('warns that the Owner role holds everything', () => {
    const { root } = create();

    expect(root.querySelector('fieldset')?.textContent).toContain('Can do everything');
  });

  it('cannot be saved twice at once', () => {
    const { fixture, root } = create();
    fixture.componentRef.setInput('saving', true);
    fixture.detectChanges();

    expect(buttonNamed(root, 'Save roles').disabled).toBe(true);
  });

  it('does not offer role changes for a deactivated account', () => {
    const { fixture, root } = create();
    fixture.componentRef.setInput('staff', staffMember({ isActive: false }));
    fixture.detectChanges();

    expect(root.querySelector('fieldset')).toBeNull();
  });

  it('follows the person when the page shows someone else', () => {
    const { fixture, root } = create();
    fixture.componentRef.setInput('staff', staffMember({ id: 12, roles: [{ id: 3, code: 'ORDER_OPS', name: 'Order-ops' }] }));
    fixture.detectChanges();

    expect(checkboxes(root).map((c) => c.checked)).toEqual([false, false, true]);
  });
});
