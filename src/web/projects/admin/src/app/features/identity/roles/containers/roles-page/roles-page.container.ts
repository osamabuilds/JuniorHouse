import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { Store } from '@ngrx/store';
import { RolesMatrixComponent } from '../../components/roles-matrix/roles-matrix.component';
import { RolesPageActions } from '../../store/roles.actions';
import { selectRoleMatrix, selectRolesError, selectRolesLoading } from '../../store/roles.selectors';

/** The role and permission matrix (staff.manage). */
@Component({
  selector: 'app-roles-page',
  imports: [RolesMatrixComponent],
  template: `
    @if (matrix(); as current) {
      <app-roles-matrix [matrix]="current" />
    } @else {
      <section class="card" aria-labelledby="rp-heading">
        <h1 id="rp-heading">Roles and permissions</h1>
        @if (error(); as failure) {
          <p class="banner banner--error" role="alert">{{ failure.message }}</p>
        } @else if (loading()) {
          <p role="status">Loading…</p>
        }
      </section>
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RolesPageContainer {
  private readonly store = inject(Store);

  readonly matrix = this.store.selectSignal(selectRoleMatrix);
  readonly loading = this.store.selectSignal(selectRolesLoading);
  readonly error = this.store.selectSignal(selectRolesError);

  constructor() {
    this.store.dispatch(RolesPageActions.opened());
  }
}
