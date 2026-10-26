import { ChangeDetectionStrategy, Component, effect, inject, untracked } from '@angular/core';
import { Store } from '@ngrx/store';
import { ActingPersonService, PaginationComponent, SectionNavComponent, inputValue } from '@shared';
import { PutawayConfirmFormComponent } from '../../components/putaway-confirm-form/putaway-confirm-form.component';
import { PutawayHistoryComponent } from '../../components/putaway-history/putaway-history.component';
import { PutawayQueueComponent } from '../../components/putaway-queue/putaway-queue.component';
import { PutawayScanComponent } from '../../components/putaway-scan/putaway-scan.component';
import { ConfirmPutawayValue, PutawayItemRowDto, PutawaySelection, ScanLookupDto, ScanPendingItemDto } from '../../models';
import { PUTAWAY_PAGE_SIZE, WAREHOUSE_LINKS } from '../../putaway.constants';
import {
  PutawayPageActions,
  selectConfirmError,
  selectError,
  selectFocusToken,
  selectItemSearch,
  selectItems,
  selectItemsPage,
  selectItemsTotal,
  selectLoading,
  selectNotice,
  selectRecords,
  selectRecordsLoading,
  selectRecordsPage,
  selectRecordsTotal,
  selectSaving,
  selectScanMessage,
  selectScanResult,
  selectScanning,
  selectSelection,
} from '../../store';
import { HasPermissionDirective } from '@core/auth';

/**
 * Putaway (docs/specs/warehouse/putaway/spec.md): scan the tag (or choose the item from the list when there is no tag yet), say which
 * bin and how many, and the stock becomes available (FR-SC-08, PUT-4, PUT-19).
 */
@Component({
  selector: 'app-putaway-page',
  imports: [HasPermissionDirective, 
    SectionNavComponent,
    PutawayScanComponent,
    PutawayQueueComponent,
    PutawayConfirmFormComponent,
    PutawayHistoryComponent,
    PaginationComponent,
  ],
  templateUrl: './putaway-page.container.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PutawayPageContainer {
  private readonly store = inject(Store);
  private readonly actingPerson = inject(ActingPersonService);

  readonly links = WAREHOUSE_LINKS;
  readonly pageSize = PUTAWAY_PAGE_SIZE;
  readonly items = this.store.selectSignal(selectItems);
  readonly itemsTotal = this.store.selectSignal(selectItemsTotal);
  readonly itemsPage = this.store.selectSignal(selectItemsPage);
  readonly itemSearch = this.store.selectSignal(selectItemSearch);
  readonly loading = this.store.selectSignal(selectLoading);
  readonly error = this.store.selectSignal(selectError);
  readonly scanning = this.store.selectSignal(selectScanning);
  readonly scanResult = this.store.selectSignal(selectScanResult);
  readonly scanMessage = this.store.selectSignal(selectScanMessage);
  readonly selection = this.store.selectSignal(selectSelection);
  readonly saving = this.store.selectSignal(selectSaving);
  readonly confirmError = this.store.selectSignal(selectConfirmError);
  readonly notice = this.store.selectSignal(selectNotice);
  readonly records = this.store.selectSignal(selectRecords);
  readonly recordsTotal = this.store.selectSignal(selectRecordsTotal);
  readonly recordsPage = this.store.selectSignal(selectRecordsPage);
  readonly recordsLoading = this.store.selectSignal(selectRecordsLoading);
  readonly focusToken = this.store.selectSignal(selectFocusToken);
  readonly defaultPerson = this.actingPerson.name;

  constructor() {
    this.store.dispatch(PutawayPageActions.opened());

    // A scan that finds exactly one thing to put away goes straight to the form, so a scan is one step (PUT-14).
    effect(() => {
      const result = this.scanResult();
      if (result && !result.gtinRetired && result.pendingItems.length === 1 && untracked(this.selection) === null) {
        this.selectScanned(result, result.pendingItems[0]);
      }
    });
  }

  scan(gtin: string): void {
    this.store.dispatch(PutawayPageActions.scanSubmitted({ gtin }));
  }

  selectScanned(result: ScanLookupDto, item: ScanPendingItemDto): void {
    this.select({
      itemId: item.itemId,
      label: `${result.skuCode} (PO ${item.poId})`,
      pending: item.pending,
      gradeCode: item.gradeCode,
      scanned: true,
      gtin: result.gtin,
      idempotencyKey: newKey(),
    });
  }

  choose(item: PutawayItemRowDto): void {
    this.select({
      itemId: item.id,
      label: `${item.skuCode ?? 'item'} (PO ${item.poId})`,
      pending: item.pending,
      gradeCode: item.gradeCode,
      scanned: false,
      gtin: null,
      idempotencyKey: newKey(),
    });
  }

  confirm(selection: PutawaySelection, value: ConfirmPutawayValue): void {
    this.actingPerson.remember(value.actingPerson);
    this.store.dispatch(PutawayPageActions.confirmSubmitted({ itemId: selection.itemId, value }));
  }

  cancelSelection(): void {
    this.store.dispatch(PutawayPageActions.selectionCleared());
    this.store.dispatch(PutawayPageActions.scanCleared());
  }

  onItemSearch(event: Event): void {
    this.store.dispatch(PutawayPageActions.itemSearchChanged({ search: inputValue(event) }));
  }

  refresh(): void {
    this.store.dispatch(PutawayPageActions.refreshRequested());
  }

  dismissNotice(): void {
    this.store.dispatch(PutawayPageActions.noticeDismissed());
  }

  changeItemsPage(page: number): void {
    this.store.dispatch(PutawayPageActions.itemsPageChanged({ page }));
  }

  changeRecordsPage(page: number): void {
    this.store.dispatch(PutawayPageActions.recordsPageChanged({ page }));
  }

  private select(selection: PutawaySelection): void {
    this.store.dispatch(PutawayPageActions.itemSelected({ selection }));
  }
}

/** A fresh key per attempt: the same attempt sent twice applies once, a new attempt is a new key (PUT-8). */
function newKey(): string {
  return crypto.randomUUID();
}
