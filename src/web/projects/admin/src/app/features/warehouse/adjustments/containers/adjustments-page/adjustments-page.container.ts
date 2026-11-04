import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { Store } from '@ngrx/store';
import { ActingPersonService, PaginationComponent, SectionNavComponent, selectValue } from '@shared';
import { AdjustmentFormComponent } from '../../components/adjustment-form/adjustment-form.component';
import { AdjustmentTableComponent } from '../../components/adjustment-table/adjustment-table.component';
import { WAREHOUSE_LINKS } from '../../adjustments.constants';
import { PostAdjustmentValue } from '../../models';
import {
  AdjustmentsPageActions,
  selectAdjustments,
  selectError,
  selectFormError,
  selectFormOpen,
  selectLoading,
  selectNotice,
  selectPage,
  selectPageSize,
  selectReasonId,
  selectReasons,
  selectReconfirm,
  selectSaving,
  selectTotal,
} from '../../store';
import { HasPermissionDirective } from '@core/auth';

/** Adjustments (docs/specs/warehouse/adjustments/spec.md): the history of stock corrections, and a form to record a new one (ADJ-0..10). */
@Component({
  selector: 'app-adjustments-page',
  imports: [HasPermissionDirective, SectionNavComponent, AdjustmentTableComponent, AdjustmentFormComponent, PaginationComponent],
  templateUrl: './adjustments-page.container.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AdjustmentsPageContainer {
  private readonly store = inject(Store);
  private readonly actingPerson = inject(ActingPersonService);

  readonly links = WAREHOUSE_LINKS;
  readonly adjustments = this.store.selectSignal(selectAdjustments);
  readonly total = this.store.selectSignal(selectTotal);
  readonly currentPage = this.store.selectSignal(selectPage);
  readonly pageSize = this.store.selectSignal(selectPageSize);
  readonly loading = this.store.selectSignal(selectLoading);
  readonly error = this.store.selectSignal(selectError);
  readonly reasonId = this.store.selectSignal(selectReasonId);
  readonly reasons = this.store.selectSignal(selectReasons);
  readonly formOpen = this.store.selectSignal(selectFormOpen);
  readonly saving = this.store.selectSignal(selectSaving);
  readonly formError = this.store.selectSignal(selectFormError);
  readonly reconfirm = this.store.selectSignal(selectReconfirm);
  readonly notice = this.store.selectSignal(selectNotice);
  readonly defaultPerson = this.actingPerson.name;

  constructor() {
    this.store.dispatch(AdjustmentsPageActions.opened());
  }

  onReasonFilter(event: Event): void {
    const value = Number(selectValue(event));
    this.store.dispatch(AdjustmentsPageActions.reasonFilterChanged({ reasonId: value > 0 ? value : null }));
  }

  refresh(): void {
    this.store.dispatch(AdjustmentsPageActions.refreshRequested());
  }

  openForm(): void {
    this.store.dispatch(AdjustmentsPageActions.formOpened());
  }

  closeForm(): void {
    this.store.dispatch(AdjustmentsPageActions.formClosed());
  }

  submit(value: PostAdjustmentValue): void {
    this.actingPerson.remember(value.actingPerson);
    this.store.dispatch(AdjustmentsPageActions.submitted({ value }));
  }

  /** The person confirmed a large change: the same attempt goes again, now marked as confirmed (ADJ-4b). */
  confirm(value: PostAdjustmentValue): void {
    this.store.dispatch(AdjustmentsPageActions.submitted({ value: { ...value, reconfirmed: true } }));
  }

  decline(): void {
    this.store.dispatch(AdjustmentsPageActions.reconfirmDeclined());
  }

  dismissNotice(): void {
    this.store.dispatch(AdjustmentsPageActions.noticeDismissed());
  }

  changePage(page: number): void {
    this.store.dispatch(AdjustmentsPageActions.pageChanged({ page }));
  }

  changePageSize(size: number): void {
    this.store.dispatch(AdjustmentsPageActions.pageSizeChanged({ pageSize: size }));
  }
}
