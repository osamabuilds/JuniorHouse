import { ChangeDetectionStrategy, Component, computed, effect, inject, input, untracked } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Store } from '@ngrx/store';
import { LookupNames } from '@features/reference/lookups';
import { ActingPersonService, AppDatePipe } from '@shared';
import { InspectionFormComponent } from '../../components/inspection-form/inspection-form.component';
import { OutcomePanelComponent } from '../../components/outcome-panel/outcome-panel.component';
import { PartDecisionFormComponent } from '../../components/part-decision-form/part-decision-form.component';
import { ReworkReturnFormComponent } from '../../components/rework-return-form/rework-return-form.component';
import { SafetyChecksPanelComponent } from '../../components/safety-checks-panel/safety-checks-panel.component';
import { SubBatchTableComponent } from '../../components/sub-batch-table/sub-batch-table.component';
import {
  BatchLineDto,
  DecideBatchValue,
  DecideSubBatchValue,
  InspectionDto,
  RecordInspectionValue,
  RecordSafetyValue,
  ReworkReturnValue,
  SubBatchDto,
} from '../../models';
import { BATCH_STATUS_LABELS, CHECK_RESULT_LABELS, CHECK_TYPES, DEFECT_TYPES, MODE_LABELS, SEVERITIES } from '../../batches.constants';
import {
  BatchPageActions,
  selectBatch,
  selectBatchError,
  selectBatchLoading,
  selectNotice,
  selectPanel,
  selectPanelError,
  selectPanelTargetId,
  selectPlan,
  selectSaving,
} from '../../store';
import { firstInspection, hasBothSafetyChecks, nextStep } from '../../utils';
import { HasPermissionDirective } from '@core/auth';

/**
 * Sprint 4: one QC batch (BRD 5.11). The batch's lines, each inspection round with its checklist and defects, the mandatory child-safety
 * checks, the outcome (whole batch or split) and what is happening to any part sent back for rework. Every action asks for the acting
 * person's name and the API judges it: a refusal is shown in plain words and nothing is saved.
 */
@Component({
  selector: 'app-batch-page',
  imports: [HasPermissionDirective, 
    RouterLink,
    AppDatePipe,
    InspectionFormComponent,
    SafetyChecksPanelComponent,
    OutcomePanelComponent,
    SubBatchTableComponent,
    ReworkReturnFormComponent,
    PartDecisionFormComponent,
  ],
  templateUrl: './batch-page.container.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class BatchPageContainer {
  private readonly store = inject(Store);
  private readonly actingPerson = inject(ActingPersonService);
  readonly names = inject(LookupNames);

  /** The batch's id from the route (withComponentInputBinding). */
  readonly id = input.required<string>();

  readonly batch = this.store.selectSignal(selectBatch);
  readonly loading = this.store.selectSignal(selectBatchLoading);
  readonly loadError = this.store.selectSignal(selectBatchError);
  readonly panel = this.store.selectSignal(selectPanel);
  readonly panelTargetId = this.store.selectSignal(selectPanelTargetId);
  readonly plan = this.store.selectSignal(selectPlan);
  readonly saving = this.store.selectSignal(selectSaving);
  readonly panelError = this.store.selectSignal(selectPanelError);
  readonly notice = this.store.selectSignal(selectNotice);
  readonly defaultPerson = this.actingPerson.name;

  readonly statusLabels = BATCH_STATUS_LABELS;
  readonly modeLabels = MODE_LABELS;
  readonly resultLabels = CHECK_RESULT_LABELS;
  readonly checkTypes = CHECK_TYPES;
  readonly defectTypes = DEFECT_TYPES;
  readonly severities = SEVERITIES;

  readonly first = computed(() => (this.batch() ? firstInspection(this.batch()!) : null));
  readonly hasSubBatches = computed(() => (this.batch()?.subBatches.length ?? 0) > 0);
  readonly safetyRecorded = computed(() => hasBothSafetyChecks(this.first()));
  readonly next = computed(() => (this.batch() ? nextStep(this.batch()!) : ''));

  /** The part the open panel is about, when it is about one. */
  readonly targetPart = computed<SubBatchDto | null>(() => this.batch()?.subBatches.find((s) => s.id === this.panelTargetId()) ?? null);
  /** The inspection whose safety checks the open panel records. */
  readonly targetInspection = computed<InspectionDto | null>(() => this.batch()?.inspections.find((i) => i.id === this.panelTargetId()) ?? null);

  /** The lines an inspection form works on: the whole batch, or the returned part being re-inspected. */
  readonly inspectionLines = computed<readonly BatchLineDto[]>(() => {
    const part = this.panel() === 'reinspect' ? this.targetPart() : null;
    return part ? part.lines.map((l) => ({ sizeId: l.sizeId, colourId: l.colourId, qty: l.qty })) : (this.batch()?.lines ?? []);
  });

  readonly scopeLines = (inspection: InspectionDto): readonly BatchLineDto[] => {
    const part = this.batch()?.subBatches.find((s) => s.id === inspection.subBatchId);
    return part ? part.lines.map((l) => ({ sizeId: l.sizeId, colourId: l.colourId, qty: l.qty })) : (this.batch()?.lines ?? []);
  };

  /** Safety results stay editable until the covered quantity is decided. */
  readonly canChangeSafety = (inspection: InspectionDto): boolean => {
    const batch = this.batch();
    if (!batch) {
      return false;
    }
    if (inspection.subBatchId === null) {
      return batch.subBatches.length === 0;
    }
    return batch.subBatches.find((s) => s.id === inspection.subBatchId)?.outcomeId === null;
  };

  constructor() {
    effect(() => {
      const id = Number(this.id());
      untracked(() => this.store.dispatch(BatchPageActions.opened({ id })));
    });
  }

  scopeName(inspection: InspectionDto): string {
    const part = this.batch()?.subBatches.find((s) => s.id === inspection.subBatchId);
    return part ? `Returned part ${part.splitNo}` : 'Whole batch';
  }

  checkLabel(id: number): string {
    return CHECK_TYPES.find((c) => c.id === id)?.label ?? '';
  }

  defectLabel(id: number): string {
    return DEFECT_TYPES.find((d) => d.id === id)?.label ?? '';
  }

  severityLabel(id: number): string {
    return SEVERITIES.find((s) => s.id === id)?.label ?? '';
  }

  openInspect(): void {
    const batch = this.batch();
    if (batch) {
      this.store.dispatch(BatchPageActions.panelOpened({ panel: 'inspect', targetId: null }));
      this.store.dispatch(BatchPageActions.planRequested({ lotQty: batch.lotQty }));
    }
  }

  openReinspect(partId: number): void {
    const part = this.batch()?.subBatches.find((s) => s.id === partId);
    if (part) {
      this.store.dispatch(BatchPageActions.panelOpened({ panel: 'reinspect', targetId: partId }));
      this.store.dispatch(BatchPageActions.planRequested({ lotQty: part.lines.reduce((sum, l) => sum + l.qty, 0) }));
    }
  }

  openPanel(panel: 'safety' | 'decide' | 'return' | 'decide-part', targetId: number | null = null): void {
    this.store.dispatch(BatchPageActions.panelOpened({ panel, targetId }));
  }

  closePanel(): void {
    this.store.dispatch(BatchPageActions.panelClosed());
  }

  recordInspection(value: RecordInspectionValue): void {
    this.actingPerson.remember(value.actingPerson);
    this.store.dispatch(BatchPageActions.inspectionSubmitted({ value }));
  }

  recordSafety(value: RecordSafetyValue): void {
    this.actingPerson.remember(value.actingPerson);
    this.store.dispatch(BatchPageActions.safetyChecksSubmitted({ value }));
  }

  decide(value: DecideBatchValue): void {
    this.actingPerson.remember(value.actingPerson);
    this.store.dispatch(BatchPageActions.decisionSubmitted({ value }));
  }

  decidePart(value: DecideSubBatchValue): void {
    this.actingPerson.remember(value.actingPerson);
    this.store.dispatch(BatchPageActions.partDecisionSubmitted({ value }));
  }

  recordReturn(value: ReworkReturnValue): void {
    this.actingPerson.remember(value.actingPerson);
    this.store.dispatch(BatchPageActions.reworkReturnSubmitted({ value }));
  }
}
