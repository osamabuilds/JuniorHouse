import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { RoleMatrixDto } from '../../models';

/**
 * Which role holds which permission (roles AC-6): one table, a column per role and a row per permission, with "Yes" and "No" written
 * out so the answer never depends on a colour or a symbol. Read-only: the roles themselves are data and change by migration.
 */
@Component({
  selector: 'app-roles-matrix',
  template: `
    <section class="card" aria-labelledby="rm-heading">
      <h1 id="rm-heading">Roles and permissions</h1>
      <p class="field-hint">
        A person can hold several roles and gets all the permissions of each. The Owner role holds every permission, including ones added
        later. To change who holds which role, open the person on the Staff page.
      </p>
      <div class="table-scroll">
        <table>
          <caption>
            Which role can do what
          </caption>
          <thead>
            <tr>
              <th scope="col">Permission</th>
              @for (role of matrix().roles; track role.id) {
                <th scope="col">
                  {{ role.name }}
                  @if (role.hasAllPermissions) {
                    <span class="field-hint">holds every permission</span>
                  }
                </th>
              }
            </tr>
          </thead>
          <tbody>
            @for (permission of matrix().permissions; track permission.code) {
              <tr>
                <th scope="row">
                  <code>{{ permission.code }}</code>
                  <span class="field-hint">{{ permission.name }}@if (permission.description) { · {{ permission.description }} }</span>
                </th>
                @for (role of matrix().roles; track role.id) {
                  <td>{{ holds(role.id, permission.code) ? 'Yes' : 'No' }}</td>
                }
              </tr>
            }
          </tbody>
        </table>
      </div>
    </section>
  `,
  styles: ':host { display: block; } .table-scroll { overflow-x: auto; }',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RolesMatrixComponent {
  readonly matrix = input.required<RoleMatrixDto>();

  private readonly held = computed(() => {
    const map = new Map<number, ReadonlySet<string>>();
    for (const role of this.matrix().roles) {
      map.set(role.id, new Set(role.permissionCodes));
    }
    return map;
  });

  holds(roleId: number, code: string): boolean {
    return this.held().get(roleId)?.has(code) ?? false;
  }
}
