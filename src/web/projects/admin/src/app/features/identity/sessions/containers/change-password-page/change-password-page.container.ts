import { ChangeDetectionStrategy, Component, computed, effect, inject } from '@angular/core';
import { Router } from '@angular/router';
import { Store } from '@ngrx/store';
import { ChangePasswordValue, SessionPageActions, selectSessionError, selectSessionSaving, selectSessionUser } from '@core/auth';
import { ChangePasswordComponent } from '../../components/change-password/change-password.component';

/** The change-password page. Once the session no longer owes a password change the person moves on to where they can work. */
@Component({
  selector: 'app-change-password-page',
  imports: [ChangePasswordComponent],
  template: `<app-change-password [saving]="saving()" [error]="error()" [forced]="forced()" (submitted)="change($event)" />`,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ChangePasswordPageContainer {
  private readonly store = inject(Store);
  private readonly router = inject(Router);

  readonly saving = this.store.selectSignal(selectSessionSaving);
  readonly error = this.store.selectSignal(selectSessionError);
  private readonly user = this.store.selectSignal(selectSessionUser);

  /** True while the account is still on a temporary password (the scope says so). */
  readonly forced = computed(() => this.user()?.scope === 'ChangePassword');

  constructor() {
    effect(() => {
      const scope = this.user()?.scope;
      if (scope && scope !== 'ChangePassword') {
        void this.router.navigateByUrl('/'); // the guard sends a session that still owes two-step setup to its own page
      }
    });
  }

  change(value: ChangePasswordValue): void {
    this.store.dispatch(SessionPageActions.changePasswordSubmitted(value));
  }
}
