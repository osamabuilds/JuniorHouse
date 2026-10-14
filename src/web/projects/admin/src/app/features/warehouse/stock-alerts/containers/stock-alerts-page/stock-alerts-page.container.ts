import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { Store } from '@ngrx/store';
import { ActingPersonService, PaginationComponent, SectionNavComponent, inputValue, selectValue } from '@shared';
import { AlertTableComponent } from '../../components/alert-table/alert-table.component';
import { ThresholdChange, ThresholdTableComponent } from '../../components/threshold-table/threshold-table.component';
import { AlertLevelFilter, AlertRowDto } from '../../models';
import { WAREHOUSE_LINKS } from '../../stock-alerts.constants';
import {
  StockAlertsPageActions,
  selectAcknowledgingId,
  selectAlerts,
  selectAlertsLoading,
  selectAlertsPage,
  selectAlertsTotal,
  selectError,
  selectLevel,
  selectNotice,
  selectPageSize,
  selectSavingVariantId,
  selectSearchText,
  selectThresholds,
  selectThresholdsLoading,
  selectThresholdsPage,
  selectThresholdsTotal,
  selectUnacknowledgedOnly,
} from '../../store';

/** Alerts (docs/specs/warehouse/stock-alerts/spec.md): variants running low or out, and the thresholds that decide it (ALR-1..8). */
@Component({
  selector: 'app-stock-alerts-page',
  imports: [SectionNavComponent, AlertTableComponent, ThresholdTableComponent, PaginationComponent],
  templateUrl: './stock-alerts-page.container.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StockAlertsPageContainer {
  private readonly store = inject(Store);
  private readonly actingPerson = inject(ActingPersonService);

  readonly links = WAREHOUSE_LINKS;
  readonly pageSize = this.store.selectSignal(selectPageSize);
  readonly alerts = this.store.selectSignal(selectAlerts);
  readonly alertsTotal = this.store.selectSignal(selectAlertsTotal);
  readonly alertsPage = this.store.selectSignal(selectAlertsPage);
  readonly alertsLoading = this.store.selectSignal(selectAlertsLoading);
  readonly level = this.store.selectSignal(selectLevel);
  readonly unacknowledgedOnly = this.store.selectSignal(selectUnacknowledgedOnly);
  readonly acknowledgingId = this.store.selectSignal(selectAcknowledgingId);
  readonly thresholds = this.store.selectSignal(selectThresholds);
  readonly thresholdsTotal = this.store.selectSignal(selectThresholdsTotal);
  readonly thresholdsPage = this.store.selectSignal(selectThresholdsPage);
  readonly thresholdsLoading = this.store.selectSignal(selectThresholdsLoading);
  readonly searchText = this.store.selectSignal(selectSearchText);
  readonly savingVariantId = this.store.selectSignal(selectSavingVariantId);
  readonly error = this.store.selectSignal(selectError);
  readonly notice = this.store.selectSignal(selectNotice);
  readonly person = this.actingPerson.name;

  /** Shown when someone acts without having said who they are. */
  readonly needName = signal(false);

  constructor() {
    this.store.dispatch(StockAlertsPageActions.opened());
  }

  onLevelChange(event: Event): void {
    this.store.dispatch(StockAlertsPageActions.levelChanged({ level: selectValue(event) as AlertLevelFilter }));
  }

  onUnacknowledgedChange(event: Event): void {
    this.store.dispatch(StockAlertsPageActions.unacknowledgedOnlyChanged({ unacknowledgedOnly: (event.target as HTMLInputElement).checked }));
  }

  onSearchInput(event: Event): void {
    this.store.dispatch(StockAlertsPageActions.searchChanged({ searchText: inputValue(event) }));
  }

  onNameInput(event: Event): void {
    this.actingPerson.remember(inputValue(event));
    this.needName.set(this.actingPerson.name().length < 2 && this.needName());
  }

  acknowledge(alert: AlertRowDto): void {
    if (!this.hasName()) {
      return;
    }

    this.store.dispatch(StockAlertsPageActions.acknowledgeSubmitted({ id: alert.id, actingPerson: this.actingPerson.name() }));
  }

  saveThreshold(change: ThresholdChange): void {
    if (!this.hasName()) {
      return;
    }

    this.store.dispatch(
      StockAlertsPageActions.thresholdSubmitted({ variantRefId: change.variantRefId, quantity: change.quantity, actingPerson: this.actingPerson.name() }),
    );
  }

  refresh(): void {
    this.store.dispatch(StockAlertsPageActions.refreshRequested());
  }

  dismissNotice(): void {
    this.store.dispatch(StockAlertsPageActions.noticeDismissed());
  }

  changeAlertsPage(page: number): void {
    this.store.dispatch(StockAlertsPageActions.alertsPageChanged({ page }));
  }

  changeThresholdsPage(page: number): void {
    this.store.dispatch(StockAlertsPageActions.thresholdsPageChanged({ page }));
  }

  private hasName(): boolean {
    const ok = this.actingPerson.name().length >= 2;
    this.needName.set(!ok);
    return ok;
  }
}
