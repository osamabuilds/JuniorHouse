import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { Store } from '@ngrx/store';
import { SessionPageActions } from '@core/auth';

/**
 * Shown to a session that owes two-step verification setup. The setup itself arrives with the mfa feature; until then the person is
 * told why they cannot go further and can sign out (never left on a blank page).
 */
@Component({
  selector: 'app-setup-required-page',
  template: `
    <section class="card setup-required" aria-labelledby="sr-heading">
      <h1 id="sr-heading">Two-step verification is required</h1>
      <p>Your account has to be protected with two-step verification before you can use the admin area. That setup is not available yet.</p>
      <p>Ask an Owner for help, then sign in again.</p>
      <div class="form-actions">
        <button type="button" class="button" (click)="signOut()">Sign out</button>
      </div>
    </section>
  `,
  styles: ':host { display: block; max-width: 32rem; margin: var(--space-6) auto; }',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SetupRequiredPageContainer {
  private readonly store = inject(Store);

  signOut(): void {
    this.store.dispatch(SessionPageActions.signOutClicked());
  }
}
