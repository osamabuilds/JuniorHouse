import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Store } from '@ngrx/store';
import { ActingPersonService, SectionNavComponent } from '@shared';
import { CountSheetComponent } from '../../components/count-sheet/count-sheet.component';
import { ReviewTableComponent } from '../../components/review-table/review-table.component';
import { COUNT_STATUS_TEXT, WAREHOUSE_LINKS } from '../../cycle-counts.constants';
import { SubmitLinesValue } from '../../models';
import {
  CycleCountsPageActions,
  selectCanConfirm,
  selectChoices,
  selectConfirming,
  selectDetail,
  selectDetailLoading,
  selectError,
  selectNotice,
  selectReasonInputs,
  selectReasons,
  selectReview,
  selectReviewLoading,
  selectSubmitting,
} from '../../store';

/**
 * One cycle count (docs/specs/warehouse/cycle-counts/spec.md): the blind count sheet, then the review where differences get a reason,
 * then confirm or cancel (CC-2, CC-4, CC-5, CC-7, CC-9).
 */
@Component({
  selector: 'app-cycle-count-page',
  imports: [SectionNavComponent, CountSheetComponent, ReviewTableComponent, RouterLink],
  templateUrl: './cycle-count-page.container.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CycleCountPageContainer {
  private readonly store = inject(Store);
  private readonly actingPerson = inject(ActingPersonService);
  private readonly id = Number(inject(ActivatedRoute).snapshot.paramMap.get('id'));

  readonly links = WAREHOUSE_LINKS;
  readonly detail = this.store.selectSignal(selectDetail);
  readonly detailLoading = this.store.selectSignal(selectDetailLoading);
  readonly submitting = this.store.selectSignal(selectSubmitting);
  readonly review = this.store.selectSignal(selectReview);
  readonly reviewLoading = this.store.selectSignal(selectReviewLoading);
  readonly choices = this.store.selectSignal(selectChoices);
  readonly reasons = this.store.selectSignal(selectReasons);
  readonly canConfirm = this.store.selectSignal(selectCanConfirm);
  readonly reasonInputs = this.store.selectSignal(selectReasonInputs);
  readonly confirming = this.store.selectSignal(selectConfirming);
  readonly error = this.store.selectSignal(selectError);
  readonly notice = this.store.selectSignal(selectNotice);
  readonly defaultPerson = this.actingPerson.name;

  readonly statusText = COUNT_STATUS_TEXT;
  readonly needName = signal(false);

  constructor() {
    this.store.dispatch(CycleCountsPageActions.detailOpened({ id: this.id }));
  }

  submitLines(value: SubmitLinesValue): void {
    this.actingPerson.remember(value.actingPerson);
    this.store.dispatch(CycleCountsPageActions.linesSubmitted({ id: this.id, value }));
  }

  openReview(): void {
    this.store.dispatch(CycleCountsPageActions.reviewRequested({ id: this.id }));
  }

  closeReview(): void {
    this.store.dispatch(CycleCountsPageActions.reviewClosed());
  }

  chooseReason(event: { key: string; reasonId: number }): void {
    this.store.dispatch(CycleCountsPageActions.reasonChosen(event));
  }

  toggleReconfirm(event: { key: string; reconfirmed: boolean }): void {
    this.store.dispatch(CycleCountsPageActions.reconfirmToggled(event));
  }

  confirm(): void {
    if (!this.hasName()) {
      return;
    }

    this.store.dispatch(CycleCountsPageActions.confirmSubmitted({ id: this.id, reasons: this.reasonInputs(), actingPerson: this.actingPerson.name() }));
  }

  cancel(): void {
    if (!this.hasName()) {
      return;
    }

    this.store.dispatch(CycleCountsPageActions.cancelSubmitted({ id: this.id, actingPerson: this.actingPerson.name() }));
  }

  dismissNotice(): void {
    this.store.dispatch(CycleCountsPageActions.noticeDismissed());
  }

  private hasName(): boolean {
    const ok = this.actingPerson.name().length >= 2;
    this.needName.set(!ok);
    return ok;
  }
}
