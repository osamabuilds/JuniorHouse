import { TestBed } from '@angular/core/testing';
import { roleOption } from '@app/testing/identity-fixtures';
import { controlsWithoutLabels, setChecked, typeInto } from '@app/testing/dom-helpers';
import { CreateStaffValue } from '../../models';
import { StaffCreateComponent } from './staff-create.component';

describe('StaffCreateComponent (spec AC-4, AC-10)', () => {
  function create() {
    const fixture = TestBed.createComponent(StaffCreateComponent);
    fixture.componentRef.setInput('roles', [roleOption(), roleOption({ id: 3, code: 'SUPPORT', name: 'Support' })]);
    const submitted = vi.fn<(value: CreateStaffValue) => void>();
    fixture.componentInstance.submitted.subscribe(submitted);
    fixture.detectChanges();
    return { fixture, root: fixture.nativeElement as HTMLElement, submitted };
  }

  const submit = (root: HTMLElement) => (root.querySelector('form') as HTMLFormElement).dispatchEvent(new Event('submit'));

  it('asks for no password: the server makes a temporary one', () => {
    const { root } = create();

    expect(root.querySelector('input[type="password"]')).toBeNull();
    expect(root.textContent).toContain('temporary password');
  });

  it('submits the user name, trimmed full name and the chosen role ids', () => {
    const { root, submitted } = create();
    typeInto(root, '#sc-user', 'amna.khan');
    typeInto(root, '#sc-name', '  Amna Khan  ');
    setChecked(root, 'input[type="checkbox"]', true);

    submit(root);

    expect(submitted).toHaveBeenCalledWith({ userName: 'amna.khan', displayName: 'Amna Khan', roleIds: [2] });
  });

  it('refuses a user name outside the allowed characters or length, and an empty full name (AC-10)', () => {
    const { fixture, root, submitted } = create();
    typeInto(root, '#sc-name', 'Amna Khan');

    for (const bad of ['', 'ab', 'Has Space', 'UPPER', 'bad!name', 'x'.repeat(41)]) {
      typeInto(root, '#sc-user', bad);
      submit(root);
    }
    expect(submitted).not.toHaveBeenCalled();
    fixture.detectChanges();
    expect(root.textContent).toContain("can't be created yet");

    typeInto(root, '#sc-user', 'a.b-c_9');
    typeInto(root, '#sc-name', '   ');
    submit(root);
    expect(submitted).not.toHaveBeenCalled();
  });

  it('shows the API reason, for example a taken name, in an alert', () => {
    const { fixture } = create();
    fixture.componentRef.setInput('error', { status: 409, message: 'That user name is taken.', fieldErrors: {} });
    fixture.detectChanges();

    expect((fixture.nativeElement as HTMLElement).querySelector('[role="alert"]')?.textContent).toContain('taken');
  });

  it('labels every control and groups the roles in a fieldset with a legend', () => {
    const { root } = create();

    expect(controlsWithoutLabels(root)).toEqual([]);
    expect(root.querySelector('fieldset legend')?.textContent).toBe('Roles');
  });
});
