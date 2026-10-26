import { ChangeDetectionStrategy, Component, OnInit, inject } from '@angular/core';
import { Store } from '@ngrx/store';
import { RecoveryCodesComponent } from '../../components/recovery-codes/recovery-codes.component';
import { MfaStatusComponent } from '../../components/mfa-status/mfa-status.component';
import { MfaPageActions } from '../../store/mfa.actions';
import { selectMfaCodes, selectMfaError, selectMfaSaving, selectMfaStatus, selectMfaStep } from '../../store/mfa.selectors';

/** The account's security page: whether two-step verification is on, how many recovery codes remain, and new codes on request. */
@Component({
  selector: 'app-security-page',
  imports: [MfaStatusComponent, RecoveryCodesComponent],
  template: `
    @if (step() === 'codes' && codes(); as list) {
      <app-recovery-codes [codes]="list" (saved)="back()" />
    } @else if (status(); as current) {
      <app-mfa-status [status]="current" [saving]="saving()" [error]="error()" (regenerateRequested)="regenerate($event)" />
    } @else {
      <section class="card" aria-labelledby="sp-heading">
        <h1 id="sp-heading">Security</h1>
        <p role="status">Loading…</p>
      </section>
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SecurityPageContainer implements OnInit {
  private readonly store = inject(Store);

  readonly step = this.store.selectSignal(selectMfaStep);
  readonly codes = this.store.selectSignal(selectMfaCodes);
  readonly status = this.store.selectSignal(selectMfaStatus);
  readonly saving = this.store.selectSignal(selectMfaSaving);
  readonly error = this.store.selectSignal(selectMfaError);

  ngOnInit(): void {
    this.store.dispatch(MfaPageActions.statusOpened());
  }

  regenerate(code: string): void {
    this.store.dispatch(MfaPageActions.regenerateSubmitted({ code }));
  }

  /** Dismissing the new codes goes back to the status view (and forgets the codes). */
  back(): void {
    this.store.dispatch(MfaPageActions.statusOpened());
  }
}
