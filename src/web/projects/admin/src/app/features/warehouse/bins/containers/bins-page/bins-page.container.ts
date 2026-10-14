import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { Store } from '@ngrx/store';
import { ActingPersonService, PaginationComponent, SectionNavComponent, inputValue } from '@shared';
import { BinFormComponent } from '../../components/bin-form/bin-form.component';
import { BinTableComponent } from '../../components/bin-table/bin-table.component';
import { WAREHOUSE_LINKS } from '../../bins.constants';
import { BinRowDto, CreateBinValue } from '../../models';
import {
  BinsPageActions,
  selectBins,
  selectError,
  selectFormError,
  selectFormOpen,
  selectLoading,
  selectNotice,
  selectPage,
  selectPageSize,
  selectSaving,
  selectTogglingId,
  selectTotal,
  selectZone,
} from '../../store';

/** Bins (docs/specs/warehouse/bins/spec.md): the shelf locations, how much each holds, and adding or switching them off (BIN-1..7). */
@Component({
  selector: 'app-bins-page',
  imports: [SectionNavComponent, BinTableComponent, BinFormComponent, PaginationComponent],
  templateUrl: './bins-page.container.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class BinsPageContainer {
  private readonly store = inject(Store);
  private readonly actingPerson = inject(ActingPersonService);

  readonly links = WAREHOUSE_LINKS;
  readonly bins = this.store.selectSignal(selectBins);
  readonly total = this.store.selectSignal(selectTotal);
  readonly currentPage = this.store.selectSignal(selectPage);
  readonly pageSize = this.store.selectSignal(selectPageSize);
  readonly loading = this.store.selectSignal(selectLoading);
  readonly error = this.store.selectSignal(selectError);
  readonly zone = this.store.selectSignal(selectZone);
  readonly formOpen = this.store.selectSignal(selectFormOpen);
  readonly saving = this.store.selectSignal(selectSaving);
  readonly formError = this.store.selectSignal(selectFormError);
  readonly togglingId = this.store.selectSignal(selectTogglingId);
  readonly notice = this.store.selectSignal(selectNotice);
  readonly defaultPerson = this.actingPerson.name;

  /** Shown when someone switches a bin on or off without having said who they are. */
  readonly needName = signal(false);

  constructor() {
    this.store.dispatch(BinsPageActions.opened());
  }

  onZoneInput(event: Event): void {
    this.store.dispatch(BinsPageActions.zoneChanged({ zone: inputValue(event).trim().toUpperCase() }));
  }

  refresh(): void {
    this.store.dispatch(BinsPageActions.refreshRequested());
  }

  openForm(): void {
    this.store.dispatch(BinsPageActions.formOpened());
  }

  closeForm(): void {
    this.store.dispatch(BinsPageActions.formClosed());
  }

  create(value: CreateBinValue): void {
    this.actingPerson.remember(value.actingPerson);
    this.store.dispatch(BinsPageActions.createSubmitted({ value }));
  }

  toggle(bin: BinRowDto): void {
    const person = this.actingPerson.name();
    if (person.length < 2) {
      this.needName.set(true);
      return;
    }

    this.needName.set(false);
    this.store.dispatch(BinsPageActions.activeToggled({ bin, actingPerson: person }));
  }

  onNameInput(event: Event): void {
    this.actingPerson.remember(inputValue(event));
    this.needName.set(this.actingPerson.name().length < 2 && this.needName());
  }

  dismissNotice(): void {
    this.store.dispatch(BinsPageActions.noticeDismissed());
  }

  changePage(page: number): void {
    this.store.dispatch(BinsPageActions.pageChanged({ page }));
  }

  changePageSize(size: number): void {
    this.store.dispatch(BinsPageActions.pageSizeChanged({ pageSize: size }));
  }
}
