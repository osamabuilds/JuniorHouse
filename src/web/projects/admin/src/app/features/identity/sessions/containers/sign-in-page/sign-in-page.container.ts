import { ChangeDetectionStrategy, Component, OnInit, effect, inject } from '@angular/core';
import { Router } from '@angular/router';
import { Store } from '@ngrx/store';
import { SessionPageActions, SignInValue, selectMfaEnded, selectMfaRequired, selectSessionError, selectSessionStatus, selectSigningIn } from '@core/auth';
import { MfaChallengeComponent } from '@features/identity/mfa';
import { SignInComponent } from '../../components/sign-in/sign-in.component';
import { safeReturnUrl } from '../../safe-return-url';

/** The sign-in page: hands the form to the session store and, once a session exists, goes where the person was heading. */
@Component({
  selector: 'app-sign-in-page',
  imports: [SignInComponent, MfaChallengeComponent],
  template: `
    @if (mfaRequired()) {
      <app-mfa-challenge [checking]="signingIn()" [error]="error()" (submitted)="verify($event)" (cancelled)="cancel()" />
    } @else {
      <app-sign-in [signingIn]="signingIn()" [error]="error()" [restarted]="restarted()" (submitted)="signIn($event)" />
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SignInPageContainer implements OnInit {
  private readonly store = inject(Store);
  private readonly router = inject(Router);

  readonly signingIn = this.store.selectSignal(selectSigningIn);
  readonly error = this.store.selectSignal(selectSessionError);
  readonly mfaRequired = this.store.selectSignal(selectMfaRequired);
  readonly restarted = this.store.selectSignal(selectMfaEnded);
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

  verify(code: string): void {
    this.store.dispatch(SessionPageActions.mfaCodeSubmitted({ code }));
  }

  cancel(): void {
    this.store.dispatch(SessionPageActions.mfaCancelled());
  }

  signIn(value: SignInValue): void {
    this.store.dispatch(SessionPageActions.signInSubmitted(value));
  }
}
