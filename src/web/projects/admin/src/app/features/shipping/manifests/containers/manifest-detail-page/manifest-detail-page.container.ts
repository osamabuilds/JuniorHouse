import { CurrencyPipe, DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, effect, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Store } from '@ngrx/store';
import { statusWords } from '../../models';
import { ManifestDetailActions, selectBusy, selectDetail, selectError, selectLoading } from '../../store';

/**
 * One manifest (docs/specs/shipping/manifests, MNF-5 to MNF-9): the parcels with order number, waybill, cash and weight, the physical count, a
 * per-parcel "handed over" box while the count is short, and the note that closes it. It prints on its own with a signature line for the courier.
 */
@Component({
  selector: 'app-manifest-detail-page',
  imports: [CurrencyPipe, DatePipe, RouterLink],
  templateUrl: './manifest-detail-page.container.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ManifestDetailPageContainer {
  private readonly store = inject(Store);

  /** From the address (`/dispatch/manifests/:id`), bound by the router. */
  readonly id = input.required<string>();

  readonly detail = this.store.selectSignal(selectDetail);
  readonly loading = this.store.selectSignal(selectLoading);
  readonly busy = this.store.selectSignal(selectBusy);
  readonly error = this.store.selectSignal(selectError);

  readonly counted = signal('');
  readonly note = signal('');
  readonly words = statusWords;

  constructor() {
    effect(() => {
      const id = Number(this.id());
      if (Number.isInteger(id) && id > 0) {
        this.store.dispatch(ManifestDetailActions.opened({ id }));
      }
    });
  }

  type(field: 'counted' | 'note', event: Event): void {
    this[field].set((event.target as HTMLInputElement | HTMLTextAreaElement).value);
  }

  submitCount(event: Event): void {
    event.preventDefault();
    const parcels = Number(this.counted());
    if (this.counted().trim() !== '' && Number.isInteger(parcels) && parcels >= 0) {
      this.store.dispatch(ManifestDetailActions.countEntered({ id: Number(this.id()), parcels }));
    }
  }

  toggle(shipmentId: number, event: Event): void {
    this.store.dispatch(ManifestDetailActions.handoverToggled({ id: Number(this.id()), shipmentId, handedOver: (event.target as HTMLInputElement).checked }));
  }

  submitResolve(event: Event): void {
    event.preventDefault();
    this.store.dispatch(ManifestDetailActions.resolveSubmitted({ id: Number(this.id()), note: this.note().trim() }));
  }

  print(): void {
    window.print();
  }
}
