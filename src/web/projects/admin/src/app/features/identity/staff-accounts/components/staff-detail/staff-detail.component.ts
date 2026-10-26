import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { DatePipe } from '@angular/common';
import { StaffDto } from '../../models';

/** One account, with the three things an administrator can do to it. Presentational: the page runs the actions. */
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
  readonly saving = input(false);

  readonly deactivate = output<void>();
  readonly reactivate = output<void>();
  readonly resetPassword = output<void>();
  readonly back = output<void>();

  roleNames(): string {
    const roles = this.staff().roles;
    return roles.length === 0 ? 'None' : roles.map((role) => role.name).join(', ');
  }
}
