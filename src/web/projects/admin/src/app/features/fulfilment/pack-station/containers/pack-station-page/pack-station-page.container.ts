import { CurrencyPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Store } from '@ngrx/store';
import { ScanBannerComponent, ScanBoxComponent } from '@shared';
import { PackLinesComponent } from '../../components/pack-lines/pack-lines.component';
import { MAX_PARCEL_GRAMS, packagingName } from '../../models';
import {
  PackStationPageActions,
  selectBlockedReason,
  selectBusy,
  selectError,
  selectLastScan,
  selectNotice,
  selectPacked,
  selectView,
  selectViewLoading,
  selectWeight,
} from '../../store';

/**
 * The pack station (docs/specs/fulfilment/pack-station, PAK-1 to PAK-13): a second, independent scan of every item, the packaging to use, the
 * weight, and sealing the parcel. A cash on delivery order shows the amount to collect large (PAK-11). A weight that does not fit the order warns
 * and needs a note to complete anyway (PAK-7).
 */
@Component({
  selector: 'app-pack-station-page',
  imports: [RouterLink, CurrencyPipe, ScanBoxComponent, ScanBannerComponent, PackLinesComponent],
  templateUrl: './pack-station-page.container.html',
  styles: ['.cash-due__amount { font-size: 3rem; font-weight: 700; margin: 0; } .cash-due { border: 3px solid var(--color-brass-dark); }'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PackStationPageContainer {
  private readonly store = inject(Store);

  /** From the address (`/fulfilment/pack/:id`), bound by the router. */
  readonly id = input.required<string>();

  readonly view = this.store.selectSignal(selectView);
  readonly loading = this.store.selectSignal(selectViewLoading);
  readonly lastScan = this.store.selectSignal(selectLastScan);
  readonly weight = this.store.selectSignal(selectWeight);
  readonly busy = this.store.selectSignal(selectBusy);
  readonly blockedReason = this.store.selectSignal(selectBlockedReason);
  readonly packed = this.store.selectSignal(selectPacked);
  readonly error = this.store.selectSignal(selectError);
  readonly notice = this.store.selectSignal(selectNotice);

  readonly grams = signal('');
  readonly note = signal('');
  readonly maxGrams = MAX_PARCEL_GRAMS;

  readonly canWork = computed(() => this.view()?.status === 'PACKING' && this.blockedReason() === null && !this.packed());
  readonly gramsNumber = computed(() => Number(this.grams()));
  readonly gramsProblem = computed(() => {
    const n = this.gramsNumber();
    return this.grams().trim() !== '' && Number.isInteger(n) && n > 0 && n <= MAX_PARCEL_GRAMS ? null : `Enter the weight in whole grams, from 1 to ${MAX_PARCEL_GRAMS}.`;
  });
  readonly weightKnown = computed(() => this.view()?.weightGrams != null);
  readonly needsNote = computed(() => this.view()?.weightDeviates === true);
  readonly canComplete = computed(() => {
    const v = this.view();
    return this.canWork() && !!v?.allScanned && this.weightKnown() && (!this.needsNote() || this.note().trim().length > 0) && !this.busy();
  });
  readonly packagingName = computed(() => packagingName(this.view()?.packaging ?? ''));

  constructor() {
    effect(() => {
      const orderId = Number(this.id());
      if (Number.isInteger(orderId) && orderId > 0) {
        this.store.dispatch(PackStationPageActions.opened({ orderId }));
      }
    });
  }

  scan(code: string): void {
    const view = this.view();
    if (view) {
      this.store.dispatch(PackStationPageActions.scanned({ orderId: view.orderId, code }));
    }
  }

  submitWeight(event: Event): void {
    event.preventDefault();
    const view = this.view();
    if (view && !this.gramsProblem()) {
      this.store.dispatch(PackStationPageActions.weightSubmitted({ orderId: view.orderId, grams: this.gramsNumber() }));
    }
  }

  type(field: 'grams' | 'note', event: Event): void {
    this[field].set((event.target as HTMLInputElement | HTMLTextAreaElement).value);
  }

  complete(): void {
    const view = this.view();
    if (view) {
      this.store.dispatch(PackStationPageActions.completeClicked({ orderId: view.orderId, note: this.note().trim() || null }));
    }
  }

  dismissNotice(): void {
    this.store.dispatch(PackStationPageActions.noticeDismissed());
  }
}
