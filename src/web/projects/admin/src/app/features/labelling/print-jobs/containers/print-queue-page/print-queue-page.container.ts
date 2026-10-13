import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute } from '@angular/router';
import { Store } from '@ngrx/store';
import { interval } from 'rxjs';
import { ActingPersonService, PaginationComponent, SectionNavComponent, selectValue } from '@shared';
import { JobTableComponent } from '../../components/job-table/job-table.component';
import { ReprintFormComponent } from '../../components/reprint-form/reprint-form.component';
import { JobStatusFilter, PrintJobDto, ReprintValue } from '../../models';
import { LABELLING_LINKS, QUEUE_REFRESH_MS } from '../../print-jobs.constants';
import {
  PrintJobsPageActions,
  selectBusyJobId,
  selectError,
  selectJobs,
  selectLoading,
  selectNotice,
  selectPage,
  selectPageSize,
  selectPrinters,
  selectReprintError,
  selectReprintSaving,
  selectReprintSku,
  selectReprintVariantId,
  selectStatus,
  selectTotal,
  selectVariantId,
} from '../../store';

/**
 * The print queue (docs/specs/labelling/print-jobs/spec.md): every job with its state, refreshed every few seconds, with Retry and
 * Cancel. Opened with a variant (<code>?variantId=</code>) it shows that SKU's print history, and with <code>&reprint=1</code> it
 * opens the reprint form for it (PJ-5, PJ-6, PJ-13).
 */
@Component({
  selector: 'app-print-queue-page',
  imports: [SectionNavComponent, JobTableComponent, ReprintFormComponent, PaginationComponent],
  templateUrl: './print-queue-page.container.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PrintQueuePageContainer {
  private readonly store = inject(Store);
  private readonly route = inject(ActivatedRoute);
  private readonly actingPerson = inject(ActingPersonService);

  readonly links = LABELLING_LINKS;
  readonly jobs = this.store.selectSignal(selectJobs);
  readonly total = this.store.selectSignal(selectTotal);
  readonly currentPage = this.store.selectSignal(selectPage);
  readonly pageSize = this.store.selectSignal(selectPageSize);
  readonly loading = this.store.selectSignal(selectLoading);
  readonly error = this.store.selectSignal(selectError);
  readonly status = this.store.selectSignal(selectStatus);
  readonly variantId = this.store.selectSignal(selectVariantId);
  readonly printers = this.store.selectSignal(selectPrinters);
  readonly reprintVariantId = this.store.selectSignal(selectReprintVariantId);
  readonly reprintSku = this.store.selectSignal(selectReprintSku);
  readonly reprintSaving = this.store.selectSignal(selectReprintSaving);
  readonly reprintError = this.store.selectSignal(selectReprintError);
  readonly busyJobId = this.store.selectSignal(selectBusyJobId);
  readonly notice = this.store.selectSignal(selectNotice);
  readonly defaultPerson = this.actingPerson.name;
  readonly showingHistory = computed(() => this.variantId() !== null);

  constructor() {
    const query = this.route.snapshot.queryParamMap;
    const rawVariant = Number(query.get('variantId'));
    const variantId = Number.isInteger(rawVariant) && rawVariant > 0 ? rawVariant : null;
    this.store.dispatch(
      PrintJobsPageActions.opened({
        variantId,
        reprint: variantId !== null && query.get('reprint') === '1',
        reprintSku: (query.get('sku') ?? '').slice(0, 120) || null,
      }),
    );

    // The queue changes by itself as the worker prints, so the screen keeps itself fresh while it is open.
    interval(QUEUE_REFRESH_MS)
      .pipe(takeUntilDestroyed())
      .subscribe(() => this.store.dispatch(PrintJobsPageActions.autoRefreshTicked()));
  }

  onStatusChange(event: Event): void {
    this.store.dispatch(PrintJobsPageActions.statusChanged({ status: selectValue(event) as JobStatusFilter }));
  }

  refresh(): void {
    this.store.dispatch(PrintJobsPageActions.refreshRequested());
  }

  showWholeQueue(): void {
    this.store.dispatch(PrintJobsPageActions.historyCleared());
  }

  retry(job: PrintJobDto): void {
    this.store.dispatch(PrintJobsPageActions.retrySubmitted({ id: job.id, actingPerson: this.person() }));
  }

  cancel(job: PrintJobDto): void {
    this.store.dispatch(PrintJobsPageActions.cancelSubmitted({ id: job.id, actingPerson: this.person() }));
  }

  reprint(variantId: number, value: ReprintValue): void {
    this.actingPerson.remember(value.actingPerson);
    this.store.dispatch(PrintJobsPageActions.reprintSubmitted({ variantId, value }));
  }

  closeReprintForm(): void {
    this.store.dispatch(PrintJobsPageActions.reprintFormClosed());
  }

  dismissNotice(): void {
    this.store.dispatch(PrintJobsPageActions.noticeDismissed());
  }

  changePage(page: number): void {
    this.store.dispatch(PrintJobsPageActions.pageChanged({ page }));
  }

  changePageSize(size: number): void {
    this.store.dispatch(PrintJobsPageActions.pageSizeChanged({ pageSize: size }));
  }

  private person(): string {
    return this.defaultPerson() || 'Warehouse staff';
  }
}
