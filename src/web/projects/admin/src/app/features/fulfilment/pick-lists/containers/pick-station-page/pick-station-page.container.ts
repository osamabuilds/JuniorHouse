import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Store } from '@ngrx/store';
import { ScanBannerComponent, ScanBoxComponent } from '@shared';
import { FlaggableLine, RaiseExceptionFormComponent, RaiseExceptionRequest } from '../../../pick-exceptions';
import { PickRowsComponent } from '../../components/pick-rows/pick-rows.component';
import {
  PickStationPageActions,
  selectBusy,
  selectError,
  selectHeldBy,
  selectLastScan,
  selectList,
  selectListLoading,
  selectNotice,
} from '../../store';

/**
 * The pick station (docs/specs/fulfilment/pick-lists, PCK-1 to PCK-12; warehouse-app, APP-3 to APP-6): the pick list in walking order and one scan
 * box that keeps the focus. The picker scans a shelf, then each item; the answer is shown large in words, an icon and a colour. A wrong item or
 * shelf is refused and nothing is counted. When every unit is scanned the pick is completed; when the shelf does not match, the picker flags the
 * line instead of forcing the pick (FR-SC-11).
 */
@Component({
  selector: 'app-pick-station-page',
  imports: [RouterLink, ScanBoxComponent, ScanBannerComponent, PickRowsComponent, RaiseExceptionFormComponent],
  templateUrl: './pick-station-page.container.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PickStationPageContainer {
  private readonly store = inject(Store);

  /** From the address (`/fulfilment/pick/:id`), bound by the router. */
  readonly id = input.required<string>();

  readonly list = this.store.selectSignal(selectList);
  readonly loading = this.store.selectSignal(selectListLoading);
  readonly lastScan = this.store.selectSignal(selectLastScan);
  readonly busy = this.store.selectSignal(selectBusy);
  readonly heldBy = this.store.selectSignal(selectHeldBy);
  readonly error = this.store.selectSignal(selectError);
  readonly notice = this.store.selectSignal(selectNotice);

  readonly flagging = signal(false);

  /** Only the holder of an order being picked can scan. Every other state reads the list and says why. */
  readonly canScan = computed(() => this.list()?.status === 'PICKING' && this.heldBy() === null);
  readonly flaggable = computed<readonly FlaggableLine[]>(() =>
    (this.list()?.rows ?? [])
      .filter((r) => r.binCode !== '' && r.status === 'ALLOCATED' && r.picked < r.quantity)
      .map((r) => ({
        key: `${r.variantId}@${r.binCode}`,
        variantId: r.variantId,
        binCode: r.binCode,
        label: `${r.name}, ${r.colour}, ${r.size} at ${r.binCode}`,
        remaining: r.quantity - r.picked,
      })),
  );

  constructor() {
    effect(() => {
      const orderId = Number(this.id());
      if (Number.isInteger(orderId) && orderId > 0) {
        this.store.dispatch(PickStationPageActions.opened({ orderId }));
      }
    });
  }

  scan(code: string): void {
    const list = this.list();
    if (list) {
      this.store.dispatch(PickStationPageActions.scanned({ orderId: list.orderId, code }));
    }
  }

  complete(): void {
    const list = this.list();
    if (list) {
      this.store.dispatch(PickStationPageActions.completeClicked({ orderId: list.orderId }));
    }
  }

  release(): void {
    const list = this.list();
    if (list) {
      this.store.dispatch(PickStationPageActions.releaseClicked({ orderId: list.orderId }));
    }
  }

  flag(request: RaiseExceptionRequest): void {
    const list = this.list();
    if (list) {
      this.store.dispatch(PickStationPageActions.exceptionFlagged({ orderId: list.orderId, request }));
      this.flagging.set(false);
    }
  }

  dismissNotice(): void {
    this.store.dispatch(PickStationPageActions.noticeDismissed());
  }
}
