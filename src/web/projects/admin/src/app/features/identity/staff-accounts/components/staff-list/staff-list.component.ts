import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { StaffDto } from '../../models';

/** The accounts table. Presentational: the page owns the data and the paging. */
@Component({
  selector: 'app-staff-list',
  template: `
    @if (loading()) {
      <p class="state-message">Loading…</p>
    } @else if (staff().length === 0) {
      <p class="state-message">No staff accounts found.</p>
    } @else {
      <table class="responsive">
        <caption class="sr-only">Staff accounts</caption>
        <thead>
          <tr>
            <th scope="col">User name</th>
            <th scope="col">Name</th>
            <th scope="col">Roles</th>
            <th scope="col">Status</th>
            <th scope="col">Actions</th>
          </tr>
        </thead>
        <tbody>
          @for (person of staff(); track person.id) {
            <tr>
              <td data-label="User name">{{ person.userName }}</td>
              <td data-label="Name">{{ person.displayName }}</td>
              <td data-label="Roles">{{ roleNames(person) }}</td>
              <td data-label="Status">
                <span class="badge" [class.badge--muted]="!person.isActive">{{ person.isActive ? 'Active' : 'Deactivated' }}</span>
                @if (person.mustChangePassword) {
                  <span class="badge">Must change password</span>
                }
              </td>
              <td data-label="Actions">
                <button type="button" class="button button--small" [attr.aria-label]="'Open ' + person.userName" (click)="selected.emit(person)">Open</button>
              </td>
            </tr>
          }
        </tbody>
      </table>
    }
  `,
  styles: ':host { display: block; }',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StaffListComponent {
  readonly staff = input.required<readonly StaffDto[]>();
  readonly loading = input.required<boolean>();

  readonly selected = output<StaffDto>();

  roleNames(person: StaffDto): string {
    return person.roles.length === 0 ? 'None' : person.roles.map((role) => role.name).join(', ');
  }
}
