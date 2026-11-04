import { ChangeDetectionStrategy, Component, OnInit, inject } from '@angular/core';
import { Store } from '@ngrx/store';
import { MfaEnrolComponent } from '../../components/mfa-enrol/mfa-enrol.component';
import { RecoveryCodesComponent } from '../../components/recovery-codes/recovery-codes.component';
import { MfaPageActions } from '../../store/mfa.actions';
import { selectMfaCodes, selectMfaEnrolment, selectMfaError, selectMfaSaving, selectMfaStep } from '../../store/mfa.selectors';

/**
 * The page a session lands on when its account still owes two-step verification (the "setup required" page): makes the secret, shows
 * it to scan, checks the first code, then shows the recovery codes once before the person moves on.
 */
@Component({
  selector: 'app-enrol-page',
  imports: [MfaEnrolComponent, RecoveryCodesComponent],
  template: `
    @switch (step()) {
      @case ('scanning') {
        @if (enrolment(); as secret) {
          <app-mfa-enrol [enrolment]="secret" [saving]="saving()" [error]="error()" (codeSubmitted)="confirm($event)" (restartRequested)="restart()" />
        }
      }
      @case ('codes') {
        @if (codes(); as list) {
          <app-recovery-codes [codes]="list" (saved)="saved()" />
        }
      }
      @default {
        <section class="card" aria-labelledby="ep-heading">
          <h1 id="ep-heading">Set up two-step verification</h1>
          @if (error(); as failure) {
            <p class="banner banner--error" role="alert">{{ failure.message }}</p>
            <button type="button" class="button" (click)="restart()">Try again</button>
          } @else {
            <p role="status">Getting your setup code ready…</p>
          }
        </section>
      }
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class EnrolPageContainer implements OnInit {
  private readonly store = inject(Store);

  readonly step = this.store.selectSignal(selectMfaStep);
  readonly enrolment = this.store.selectSignal(selectMfaEnrolment);
  readonly codes = this.store.selectSignal(selectMfaCodes);
  readonly saving = this.store.selectSignal(selectMfaSaving);
  readonly error = this.store.selectSignal(selectMfaError);

  ngOnInit(): void {
    this.store.dispatch(MfaPageActions.opened());
  }

  confirm(code: string): void {
    this.store.dispatch(MfaPageActions.codeSubmitted({ code }));
  }

  restart(): void {
    this.store.dispatch(MfaPageActions.restartClicked());
  }

  saved(): void {
    this.store.dispatch(MfaPageActions.codesSaved());
  }
}
