import { ChangeDetectionStrategy, Component, inject, input, output, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ApiError } from '@core/http';
import { ControlErrorsComponent, FieldErrorsComponent, inputChecked } from '@shared';
import { CreateStaffValue, RoleDto } from '../../models';

/** The user name rule of the spec (AC-10): lower-case letters, digits, dot, underscore and hyphen, 3 to 40 characters. */
export const USER_NAME_PATTERN = /^[a-z0-9._-]{3,40}$/;

/**
 * Creates a staff account. The person gets a server-generated temporary password, shown once afterwards, so this form asks for no
 * password at all. The roles are optional here; an account with none can sign in and see nothing.
 */
@Component({
  selector: 'app-staff-create',
  imports: [ReactiveFormsModule, ControlErrorsComponent, FieldErrorsComponent],
  template: `
    <form class="card" [formGroup]="form" (ngSubmit)="submit()" aria-labelledby="sc-heading" novalidate>
      <h2 id="sc-heading">Add a staff member</h2>
      <p class="field-hint">A temporary password is made for them and shown to you once. They must change it when they first sign in.</p>

      @if (error()) {
        <p class="banner banner--error" role="alert">{{ error()?.message }}</p>
      }
      @if (summary()) {
        <p class="banner banner--error" role="alert">{{ summary() }}</p>
      }

      <div class="form-grid">
        <div class="form-field">
          <label for="sc-user">User name</label>
          <input id="sc-user" type="text" formControlName="userName" maxlength="40" autocomplete="off" autocapitalize="none" spellcheck="false" />
          <p class="field-hint">Lower-case letters, digits, dot, underscore and hyphen. It cannot be changed later.</p>
          <app-control-errors [control]="form.controls.userName" label="User name" />
          <app-field-errors [messages]="fieldErrors('UserName')" />
        </div>
        <div class="form-field">
          <label for="sc-name">Full name</label>
          <input id="sc-name" type="text" formControlName="displayName" maxlength="100" autocomplete="off" />
          <app-control-errors [control]="form.controls.displayName" label="Full name" />
          <app-field-errors [messages]="fieldErrors('DisplayName')" />
        </div>
      </div>

      <fieldset class="form-field">
        <legend>Roles</legend>
        @for (role of roles(); track role.id) {
          <label class="toolbar__checkbox">
            <input type="checkbox" [checked]="isChosen(role.id)" (change)="toggle(role.id, $event)" />
            {{ role.name }}
            @if (role.description) {
              <span class="field-hint">{{ role.description }}</span>
            }
            @if (role.hasAllPermissions) {
              <span class="field-hint">Can do everything.</span>
            }
          </label>
        } @empty {
          <p class="field-hint">No roles are available.</p>
        }
        <app-field-errors [messages]="fieldErrors('RoleIds')" />
      </fieldset>

      <div class="form-actions">
        <button type="submit" class="button button--primary" [disabled]="saving()">{{ saving() ? 'Creating…' : 'Create account' }}</button>
        <button type="button" class="button" (click)="cancelled.emit()">Cancel</button>
      </div>
    </form>
  `,
  styles: ':host { display: block; margin-bottom: var(--space-5); }',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StaffCreateComponent {
  private readonly formBuilder = inject(FormBuilder);

  readonly roles = input.required<readonly RoleDto[]>();
  readonly saving = input(false);
  readonly error = input<ApiError | null>(null);

  readonly submitted = output<CreateStaffValue>();
  readonly cancelled = output<void>();

  readonly summary = signal<string | null>(null);
  private readonly chosen = signal<readonly number[]>([]);

  readonly form = this.formBuilder.nonNullable.group({
    userName: ['', [Validators.required, Validators.pattern(USER_NAME_PATTERN)]],
    displayName: ['', [Validators.required, Validators.pattern(/\S/), Validators.maxLength(100)]],
  });

  isChosen(roleId: number): boolean {
    return this.chosen().includes(roleId);
  }

  toggle(roleId: number, event: Event): void {
    this.chosen.set(inputChecked(event) ? [...this.chosen(), roleId] : this.chosen().filter((id) => id !== roleId));
  }

  fieldErrors(field: string): readonly string[] {
    return this.error()?.fieldErrors[field] ?? [];
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.summary.set("This can't be created yet. Check the user name and the full name.");
      return;
    }

    this.summary.set(null);
    const value = this.form.getRawValue();
    this.submitted.emit({ userName: value.userName, displayName: value.displayName.trim(), roleIds: this.chosen() });
  }
}
