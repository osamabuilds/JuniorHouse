import { ChangeDetectionStrategy, Component, computed, effect, inject, input, untracked } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Store } from '@ngrx/store';
import { LookupNames } from '@features/reference/lookups';
import { ActingPersonService, AppDatePipe } from '@shared';
import { DeliveryNoteFormComponent } from '../../components/delivery-note-form/delivery-note-form.component';
import { DeliveryNoteListComponent } from '../../components/delivery-note-list/delivery-note-list.component';
import { ExpectedCompletionPanelComponent } from '../../components/expected-completion-panel/expected-completion-panel.component';
import { MilestoneFormComponent } from '../../components/milestone-form/milestone-form.component';
import { MilestoneTimelineComponent } from '../../components/milestone-timeline/milestone-timeline.component';
import { PpRoundPanelComponent } from '../../components/pp-round-panel/pp-round-panel.component';
import { RunFlagBadgesComponent } from '../../components/run-flag-badges/run-flag-badges.component';
import {
  DecideSampleValue,
  DeliveryNoteValue,
  ExpectedCompletionValue,
  RecordMilestoneValue,
  SampleFileUpload,
  SubmitSampleValue,
} from '../../models';
import { RUN_STATUS_LABELS, RUN_STATUS_OPEN } from '../../production-runs.constants';
import { ProductionApiService } from '../../services/production-api.service';
import {
  ProductionRunActions,
  selectFileError,
  selectFileUploading,
  selectNotice,
  selectPanel,
  selectPanelError,
  selectRun,
  selectRunError,
  selectRunLoading,
  selectSaving,
} from '../../store';
import { runFlags } from '../../utils';

/**
 * Sprint 3: one production run. The milestone timeline, the pre-production sample rounds, the expected completion date and
 * the delivery notes, each with its own action. Every action asks for the acting person's name (kept for next time in this
 * browser, since there is no sign-in until Sprint 8) and the API judges it: a refusal is shown in plain words and nothing is saved.
 */
@Component({
  selector: 'app-production-run-page',
  imports: [
    RouterLink,
    AppDatePipe,
    RunFlagBadgesComponent,
    MilestoneTimelineComponent,
    MilestoneFormComponent,
    PpRoundPanelComponent,
    ExpectedCompletionPanelComponent,
    DeliveryNoteFormComponent,
    DeliveryNoteListComponent,
  ],
  templateUrl: './production-run-page.container.html',
  styleUrl: './production-run-page.container.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProductionRunPageContainer {
  private readonly store = inject(Store);
  private readonly api = inject(ProductionApiService);
  private readonly actingPerson = inject(ActingPersonService);
  readonly names = inject(LookupNames);

  /** The run's id from the route (withComponentInputBinding). */
  readonly id = input.required<string>();

  readonly run = this.store.selectSignal(selectRun);
  readonly loading = this.store.selectSignal(selectRunLoading);
  readonly loadError = this.store.selectSignal(selectRunError);
  readonly panel = this.store.selectSignal(selectPanel);
  readonly saving = this.store.selectSignal(selectSaving);
  readonly panelError = this.store.selectSignal(selectPanelError);
  readonly notice = this.store.selectSignal(selectNotice);
  readonly fileUploading = this.store.selectSignal(selectFileUploading);
  readonly fileError = this.store.selectSignal(selectFileError);

  readonly defaultPerson = this.actingPerson.name;
  readonly statusLabels = RUN_STATUS_LABELS;
  readonly isOpen = computed(() => this.run()?.statusId === RUN_STATUS_OPEN);
  readonly flags = computed(() => {
    const run = this.run();
    if (!run) {
      return [];
    }
    const notes = run.deliveryNotes;
    return runFlags({
      ...run,
      hasOverDelivery: notes.some((note) => note.quantityStatusCode === 'Over'),
      hasShortDelivery: notes.some((note) => note.quantityStatusCode === 'Short'),
      hasLateDelivery: notes.some((note) => note.timingCode !== 'OnTime'),
    });
  });

  readonly fabricName = computed(() => this.names.fabricName(this.run()?.fabricResponsibilityId ?? null));

  readonly sampleFileUrlFor = (roundNo: number, fileId: number): string => this.api.sampleFileUrl(this.run()?.id ?? 0, roundNo, fileId);

  constructor() {
    effect(() => {
      const id = Number(this.id());
      untracked(() => this.store.dispatch(ProductionRunActions.opened({ id })));
    });
  }

  openPanel(panel: 'milestone' | 'sample' | 'expected' | 'delivery'): void {
    this.store.dispatch(ProductionRunActions.panelOpened({ panel }));
  }

  closePanel(): void {
    this.store.dispatch(ProductionRunActions.panelClosed());
  }

  recordMilestone(value: RecordMilestoneValue): void {
    this.actingPerson.remember(value.actingPerson);
    this.store.dispatch(ProductionRunActions.milestoneSubmitted({ value }));
  }

  submitSample(value: SubmitSampleValue): void {
    this.actingPerson.remember(value.actingPerson);
    this.store.dispatch(ProductionRunActions.sampleSubmitted({ value }));
  }

  decideSample(value: DecideSampleValue): void {
    this.actingPerson.remember(value.actingPerson);
    this.store.dispatch(ProductionRunActions.sampleDecided({ value }));
  }

  setExpectedCompletion(value: ExpectedCompletionValue): void {
    this.actingPerson.remember(value.actingPerson);
    this.store.dispatch(ProductionRunActions.expectedCompletionSubmitted({ value }));
  }

  recordDeliveryNote(value: DeliveryNoteValue): void {
    this.actingPerson.remember(value.actingPerson);
    this.store.dispatch(ProductionRunActions.deliveryNoteSubmitted({ value }));
  }

  uploadFile(upload: SampleFileUpload): void {
    this.store.dispatch(ProductionRunActions.sampleFileUploadRequested({ upload }));
  }

  removeFile(request: { roundNo: number; fileId: number }): void {
    this.store.dispatch(ProductionRunActions.sampleFileRemoveRequested(request));
  }
}
