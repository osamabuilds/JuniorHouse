import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { Store } from '@ngrx/store';
import { ActingPersonService, PaginationComponent, SectionNavComponent, inputValue, selectValue } from '@shared';
import { BinPickerComponent } from '../../components/bin-picker/bin-picker.component';
import { CountListComponent } from '../../components/count-list/count-list.component';
import { WAREHOUSE_LINKS } from '../../cycle-counts.constants';
import { CountStatusFilter } from '../../models';
import {
  CycleCountsPageActions,
  selectBins,
  selectBinsLoading,
  selectBinZone,
  selectBinsPage,
  selectBinsTotal,
  selectCounts,
  selectCountsLoading,
  selectCountsPage,
  selectCountsTotal,
  selectCreating,
  selectError,
  selectNotice,
  selectPageSize,
  selectSelectedBinIds,
  selectStatusFilter,
} from '../../store';
import { HasPermissionDirective } from '@core/auth';

/** Cycle counts (docs/specs/warehouse/cycle-counts/spec.md): the counts so far, and choosing bins to start a new one (CC-1, CC-3, CC-11). */
@Component({
  selector: 'app-cycle-counts-page',
  imports: [HasPermissionDirective, SectionNavComponent, CountListComponent, BinPickerComponent, PaginationComponent],
  templateUrl: './cycle-counts-page.container.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CycleCountsPageContainer {
  private readonly store = inject(Store);
  private readonly actingPerson = inject(ActingPersonService);

  readonly links = WAREHOUSE_LINKS;
  readonly pageSize = this.store.selectSignal(selectPageSize);
  readonly counts = this.store.selectSignal(selectCounts);
  readonly countsTotal = this.store.selectSignal(selectCountsTotal);
  readonly countsPage = this.store.selectSignal(selectCountsPage);
  readonly countsLoading = this.store.selectSignal(selectCountsLoading);
  readonly statusFilter = this.store.selectSignal(selectStatusFilter);
  readonly bins = this.store.selectSignal(selectBins);
  readonly binsTotal = this.store.selectSignal(selectBinsTotal);
  readonly binsPage = this.store.selectSignal(selectBinsPage);
  readonly binsLoading = this.store.selectSignal(selectBinsLoading);
  readonly binZone = this.store.selectSignal(selectBinZone);
  readonly selected = this.store.selectSignal(selectSelectedBinIds);
  readonly creating = this.store.selectSignal(selectCreating);
  readonly error = this.store.selectSignal(selectError);
  readonly notice = this.store.selectSignal(selectNotice);
  readonly person = this.actingPerson.name;

  readonly needName = signal(false);

  constructor() {
    this.store.dispatch(CycleCountsPageActions.listOpened());
  }

  onStatusChange(event: Event): void {
    this.store.dispatch(CycleCountsPageActions.statusFilterChanged({ status: selectValue(event) as CountStatusFilter }));
  }

  onNameInput(event: Event): void {
    this.actingPerson.remember(inputValue(event));
    this.needName.set(this.actingPerson.name().length < 2 && this.needName());
  }

  onBinZoneInput(event: Event): void {
    this.store.dispatch(CycleCountsPageActions.binZoneChanged({ zone: inputValue(event).trim().toUpperCase() }));
  }

  toggleBin(binId: number): void {
    this.store.dispatch(CycleCountsPageActions.binToggled({ binId }));
  }

  start(): void {
    if (this.actingPerson.name().length < 2) {
      this.needName.set(true);
      return;
    }

    this.needName.set(false);
    this.store.dispatch(CycleCountsPageActions.createSubmitted({ actingPerson: this.actingPerson.name() }));
  }

  dismissNotice(): void {
    this.store.dispatch(CycleCountsPageActions.noticeDismissed());
  }

  changeCountsPage(page: number): void {
    this.store.dispatch(CycleCountsPageActions.countsPageChanged({ page }));
  }

  changeBinsPage(page: number): void {
    this.store.dispatch(CycleCountsPageActions.binsPageChanged({ page }));
  }
}
