import { CurrencyPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Store } from '@ngrx/store';
import { parseStatementText } from '../../models';
import { CodRemittanceActions, selectBusy, selectError } from '../../store';

/**
 * Record a courier cash statement (docs/specs/shipping/cod-reconciliation, COD-8, COD-16): the reference, the date, the total the courier says it paid,
 * and one row per parcel pasted from its statement. The total is checked against the rows here and again by the server.
 */
@Component({
  selector: 'app-remittance-form-page',
  imports: [CurrencyPipe, RouterLink],
  templateUrl: './remittance-form-page.container.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RemittanceFormPageContainer {
  private readonly store = inject(Store);

  readonly busy = this.store.selectSignal(selectBusy);
  readonly error = this.store.selectSignal(selectError);

  readonly courier = signal('PRIMARY');
  readonly reference = signal('');
  readonly remittedOn = signal(new Date().toISOString().slice(0, 10));
  readonly statedTotal = signal('');
  readonly text = signal('');

  readonly parsed = computed(() => parseStatementText(this.text()));
  readonly totalNumber = computed(() => Number(this.statedTotal().replace(/,/g, '')));
  readonly totalAgrees = computed(() => this.statedTotal().trim() !== '' && this.totalNumber() === this.parsed().total);
  readonly canSave = computed(
    () => this.reference().trim() !== '' && this.parsed().lines.length > 0 && this.parsed().problems.length === 0 && this.totalAgrees() && this.remittedOn() !== '',
  );

  constructor() {
    this.store.dispatch(CodRemittanceActions.formOpened());
  }

  type(field: 'courier' | 'reference' | 'remittedOn' | 'statedTotal' | 'text', event: Event): void {
    this[field].set((event.target as HTMLInputElement | HTMLTextAreaElement | HTMLSelectElement).value);
  }

  submit(event: Event): void {
    event.preventDefault();
    if (!this.canSave()) {
      return;
    }

    this.store.dispatch(
      CodRemittanceActions.recordSubmitted({
        request: {
          courier: this.courier(),
          reference: this.reference().trim(),
          remittedOn: this.remittedOn(),
          statedTotal: this.totalNumber(),
          lines: this.parsed().lines,
        },
      }),
    );
  }
}
