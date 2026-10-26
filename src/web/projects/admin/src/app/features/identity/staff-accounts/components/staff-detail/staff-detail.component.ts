import { ChangeDetectionStrategy, Component, input, linkedSignal, output } from '@angular/core';
import { DatePipe } from '@angular/common';
import { inputChecked } from '@shared';
import { RoleDto, StaffDto } from '../../models';

/** One account, with what an administrator can do to it: change its roles, deactivate it, reset its password. Presentational: the page runs the actions. */
@Component({
  selector: 'app-staff-detail',
  imports: [DatePipe],
  template: `
    <article class="card" aria-labelledby="sd-heading">
      <h2 id="sd-heading">{{ staff().displayName }}</h2>
      <dl>
        <dt>User name</dt>
        <dd>{{ staff().userName }}</dd>
        <dt>Status</dt>
        <dd>{{ staff().isActive ? 'Active' : 'Deactivated' }}</dd>
        <dt>Roles</dt>
        <dd>{{ roleNames() }}</dd>
        <dt>Password</dt>
        <dd>
          @if (staff().hasTemporaryPassword) {
            A temporary password is outstanding; it lapses on {{ staff().temporaryPasswordExpiresAt | date: 'medium' }}.
          } @else if (staff().mustChangePassword) {
            Must be changed at the next sign-in.
          } @else {
            Set by the person.
          }
        </dd>
        <dt>Created</dt>
        <dd>{{ staff().createdAt | date: 'medium' }}</dd>
      </dl>

      @if (staff().isActive && roles().length > 0) {
        <fieldset class="form-field">
          <legend>Roles</legend>
          @for (role of roles(); track role.id) {
            <label class="toolbar__checkbox">
              <input type="checkbox" [checked]="chosen().includes(role.id)" (change)="toggle(role.id, $event)" />
              {{ role.name }}
              @if (role.description) {
                <span class="field-hint">{{ role.description }}</span>
              }
              @if (role.hasAllPermissions) {
                <span class="field-hint">Can do everything.</span>
              }
            </label>
          }
          <p class="field-hint">A role gives a set of permissions; a person gets all the permissions of every role they hold. The change applies on their next request.</p>
          <div class="form-actions">
            <button type="button" class="button" [disabled]="saving()" (click)="rolesSaved.emit(chosen())">Save roles</button>
          </div>
        </fieldset>
      }

      <div class="form-actions">
        @if (staff().isActive) {
          <button type="button" class="button" [disabled]="saving()" (click)="deactivate.emit()">Deactivate</button>
        } @else {
          <button type="button" class="button" [disabled]="saving()" (click)="reactivate.emit()">Reactivate</button>
        }
        <button type="button" class="button" [disabled]="saving() || !staff().isActive" (click)="resetPassword.emit()">Make a new temporary password</button>
        <button type="button" class="button" (click)="back.emit()">Back to the list</button>
      </div>
    </article>
  `,
  styles: ':host { display: block; margin-bottom: var(--space-5); }',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StaffDetailComponent {
  readonly staff = input.required<StaffDto>();
  readonly roles = input<readonly RoleDto[]>([]);
  readonly saving = input(false);

  readonly deactivate = output<void>();
  readonly reactivate = output<void>();
  readonly resetPassword = output<void>();
  readonly rolesSaved = output<readonly number[]>();
  readonly back = output<void>();

  /** The roles ticked in the picker: starts as the roles the person holds, and starts again whenever the page shows someone else. */
  readonly chosen = linkedSignal<readonly number[]>(() => this.staff().roles.map((role) => role.id));

  roleNames(): string {
    const roles = this.staff().roles;
    return roles.length === 0 ? 'None' : roles.map((role) => role.name).join(', ');
  }

  toggle(roleId: number, event: Event): void {
    this.chosen.update((ids) => (inputChecked(event) ? [...ids.filter((id) => id !== roleId), roleId].sort((a, b) => a - b) : ids.filter((id) => id !== roleId)));
  }
}
