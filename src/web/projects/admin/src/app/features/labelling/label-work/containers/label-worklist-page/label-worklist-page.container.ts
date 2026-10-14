import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { Store } from '@ngrx/store';
import { ActingPersonService, PaginationComponent, SectionNavComponent, selectValue } from '@shared';
import { QueueTagsFormComponent } from '../../components/queue-tags-form/queue-tags-form.component';
import { WorklistTableComponent } from '../../components/worklist-table/worklist-table.component';
import { LABELLING_LINKS } from '../../label-work.constants';
import { LabelWorkItemDto, QueueTagsValue, WorkStatusFilter } from '../../models';
import { LabelWorkApiService } from '../../services/label-work-api.service';
import {
  LabelWorkPageActions,
  selectError,
  selectItems,
  selectLoading,
  selectNotice,
  selectPage,
  selectPageSize,
  selectPriceFlagOnly,
  selectPrinters,
  selectQueueError,
  selectQueueItem,
  selectQueueSaving,
  selectResolvingId,
  selectStatus,
  selectTotal,
} from '../../store';

/** Accepted batches awaiting tags (docs/specs/labelling/label-work/spec.md): filter, queue tags, resolve parked items (LW-4, LW-5, LW-7). */
@Component({
  selector: 'app-label-worklist-page',
  imports: [SectionNavComponent, WorklistTableComponent, QueueTagsFormComponent, PaginationComponent],
  templateUrl: './label-worklist-page.container.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LabelWorklistPageContainer {
  private readonly store = inject(Store);
  private readonly api = inject(LabelWorkApiService);
  private readonly actingPerson = inject(ActingPersonService);

  readonly links = LABELLING_LINKS;
  readonly items = this.store.selectSignal(selectItems);
  readonly total = this.store.selectSignal(selectTotal);
  readonly currentPage = this.store.selectSignal(selectPage);
  readonly pageSize = this.store.selectSignal(selectPageSize);
  readonly loading = this.store.selectSignal(selectLoading);
  readonly error = this.store.selectSignal(selectError);
  readonly status = this.store.selectSignal(selectStatus);
  readonly priceFlagOnly = this.store.selectSignal(selectPriceFlagOnly);
  readonly printers = this.store.selectSignal(selectPrinters);
  readonly queueItem = this.store.selectSignal(selectQueueItem);
  readonly queueSaving = this.store.selectSignal(selectQueueSaving);
  readonly queueError = this.store.selectSignal(selectQueueError);
  readonly resolvingId = this.store.selectSignal(selectResolvingId);
  readonly notice = this.store.selectSignal(selectNotice);
  readonly defaultPerson = this.actingPerson.name;

  readonly previewUrlFor = (id: number): string => this.api.previewUrl(id);

  constructor() {
    this.store.dispatch(LabelWorkPageActions.opened());
  }

  onStatusChange(event: Event): void {
    this.store.dispatch(LabelWorkPageActions.statusChanged({ status: selectValue(event) as WorkStatusFilter }));
  }

  togglePriceFlag(): void {
    this.store.dispatch(LabelWorkPageActions.priceFlagToggled());
  }

  refresh(): void {
    this.store.dispatch(LabelWorkPageActions.refreshRequested());
  }

  openQueueForm(item: LabelWorkItemDto): void {
    this.store.dispatch(LabelWorkPageActions.queueFormOpened({ item }));
  }

  closeQueueForm(): void {
    this.store.dispatch(LabelWorkPageActions.queueFormClosed());
  }

  queue(item: LabelWorkItemDto, value: QueueTagsValue): void {
    this.actingPerson.remember(value.actingPerson);
    this.store.dispatch(LabelWorkPageActions.queueSubmitted({ itemId: item.id, value }));
  }

  resolve(item: LabelWorkItemDto, confirmRetiredStyle: boolean): void {
    const person = this.defaultPerson() || 'Warehouse staff';
    this.store.dispatch(LabelWorkPageActions.resolveSubmitted({ itemId: item.id, value: { confirmRetiredStyle, actingPerson: person } }));
  }

  dismissNotice(): void {
    this.store.dispatch(LabelWorkPageActions.noticeDismissed());
  }

  changePage(page: number): void {
    this.store.dispatch(LabelWorkPageActions.pageChanged({ page }));
  }

  changePageSize(size: number): void {
    this.store.dispatch(LabelWorkPageActions.pageSizeChanged({ pageSize: size }));
  }
}
