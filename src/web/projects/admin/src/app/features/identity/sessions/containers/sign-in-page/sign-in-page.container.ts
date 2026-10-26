import { ChangeDetectionStrategy, Component, OnInit, effect, inject } from '@angular/core';
import { Router } from '@angular/router';
import { Store } from '@ngrx/store';
import { SessionPageActions, SignInValue, selectMfaRequired, selectSessionError, selectSessionStatus, selectSigningIn } from '@core/auth';
import { SignInComponent } from '../../components/sign-in/sign-in.component';
import { safeReturnUrl } from '../../safe-return-url';

/** The sign-in page: hands the form to the session store and, once a session exists, goes where the person was heading. */
@Component({
  selector: 'app-sign-in-page',
  imports: [SignInComponent],
  template: `<app-sign-in [signingIn]="signingIn()" [error]="error()" [mfaRequired]="mfaRequired()" (submitted)="signIn($event)" />`,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SignInPageContainer implements OnInit {
  private readonly store = inject(Store);
  private readonly router = inject(Router);

  readonly signingIn = this.store.selectSignal(selectSigningIn);
  readonly error = this.store.selectSignal(selectSessionError);
  readonly mfaRequired = this.store.selectSignal(selectMfaRequired);
  private readonly status = this.store.selectSignal(selectSessionStatus);

  constructor() {
    effect(() => {
      if (this.status() === 'authenticated') {
        void this.router.navigateByUrl(safeReturnUrl(this.router.parseUrl(this.router.url).queryParams['returnUrl']));
      }
    });
  }

  ngOnInit(): void {
    this.store.dispatch(SessionPageActions.signInOpened());
  }

  signIn(value: SignInValue): void {
    this.store.dispatch(SessionPageActions.signInSubmitted(value));
  }
}
