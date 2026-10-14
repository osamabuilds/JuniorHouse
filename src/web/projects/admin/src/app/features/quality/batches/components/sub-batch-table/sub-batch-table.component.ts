import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { LookupNameResolver } from '@features/reference/lookups';
import { QcBatchDto, SubBatchDto } from '../../models';
import {
  BATCH_AWAITING_REWORK,
  BATCH_IN_INSPECTION,
  BATCH_PENDING,
  OUTCOME_ACCEPTED,
  OUTCOME_CONCESSION,
  OUTCOME_REJECTED,
  OUTCOME_RESULT_LABELS,
  PART_STATUS_LABELS,
} from '../../batches.constants';
import { hasBothSafetyChecks, inspectionOfPart, outstandingQty } from '../../utils';

/**
 * The parts of a decided batch and what each needs next (spec AC-20..AC-27): which part was accepted, rejected or sent back, what
 * is still with the vendor, and the one action that moves a returned part on (record the return, re-inspect it, record its safety
 * checks, decide it). Every state is written out; colour is only a second cue. Holds no state.
 */
@Component({
  selector: 'app-sub-batch-table',
  template: `
    <table class="responsive">
      <caption class="sr-only">
        Parts of the batch
      </caption>
      <thead>
        <tr>
          <th scope="col">Part</th>
          <th scope="col">Pieces</th>
          <th scope="col">Outcome</th>
          <th scope="col">Rework rounds</th>
          <th scope="col">Still with the vendor</th>
          <th scope="col"><span class="sr-only">Action</span></th>
        </tr>
      </thead>
      <tbody>
        @for (part of batch().subBatches; track part.id) {
          <tr>
            <td data-label="Part">
              Part {{ part.splitNo }}
              @if (part.parentSubBatchId) {
                <span class="field-hint">(came back from rework)</span>
              }
            </td>
            <td data-label="Pieces">
              {{ pieces(part) }}
              <span class="field-hint">{{ lineText(part) }}</span>
            </td>
            <td data-label="Outcome">
              @if (part.outcomeId) {
                <span class="qc-flag" [class.qc-flag--ok]="isReleased(part)" [class.qc-flag--danger]="part.outcomeId === rejected" [class.qc-flag--warn]="!isReleased(part) && part.outcomeId !== rejected">
                  {{ resultLabels[part.outcomeId] }}
                </span>
                @if (part.reason) {
                  <span class="field-hint"> {{ part.reason }}</span>
                }
              } @else {
                <span class="qc-flag qc-flag--warn">{{ statusLabels[part.statusId] }}</span>
              }
            </td>
            <td data-label="Rework rounds">{{ part.reworkRoundCount }}</td>
            <td data-label="Still with the vendor">{{ outstanding(part) > 0 ? outstanding(part) + ' pieces' : '—' }}</td>
            <td data-label="Action" class="actions-cell">
              @if (part.statusId === awaitingRework && outstanding(part) > 0) {
                <button type="button" class="button button--small" (click)="returnRequested.emit(part.id)">Record return</button>
              } @else if (part.statusId === pending) {
                <button type="button" class="button button--small" (click)="reinspectRequested.emit(part.id)">Re-inspect</button>
              } @else if (part.statusId === inInspection) {
                @if (!safetyDone(part)) {
                  <button type="button" class="button button--small" (click)="safetyRequested.emit(inspectionId(part))">Record safety checks</button>
                }
                <button type="button" class="button button--small" (click)="decideRequested.emit(part.id)">Record outcome</button>
              }
            </td>
          </tr>
        }
      </tbody>
    </table>
  `,
  styles: ':host { display: block; }',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SubBatchTableComponent {
  readonly batch = input.required<QcBatchDto>();
  readonly names = input.required<LookupNameResolver>();

  readonly returnRequested = output<number>();
  readonly reinspectRequested = output<number>();
  readonly decideRequested = output<number>();
  /** The inspection whose safety checks to record. */
  readonly safetyRequested = output<number>();

  readonly resultLabels = OUTCOME_RESULT_LABELS;
  readonly statusLabels = PART_STATUS_LABELS;
  readonly awaitingRework = BATCH_AWAITING_REWORK;
  readonly pending = BATCH_PENDING;
  readonly inInspection = BATCH_IN_INSPECTION;
  readonly rejected = OUTCOME_REJECTED;

  pieces(part: SubBatchDto): number {
    return part.lines.reduce((sum, l) => sum + l.qty, 0);
  }

  lineText(part: SubBatchDto): string {
    return part.lines.map((l) => `${this.names().sizeName(l.sizeId)} / ${this.names().colourName(l.colourId)}: ${l.qty}`).join('; ');
  }

  isReleased(part: SubBatchDto): boolean {
    return part.outcomeId === OUTCOME_ACCEPTED || part.outcomeId === OUTCOME_CONCESSION;
  }

  outstanding(part: SubBatchDto): number {
    return part.outcomeId === 4 ? outstandingQty(part) : 0;
  }

  safetyDone(part: SubBatchDto): boolean {
    return hasBothSafetyChecks(inspectionOfPart(this.batch(), part));
  }

  inspectionId(part: SubBatchDto): number {
    return inspectionOfPart(this.batch(), part)?.id ?? 0;
  }
}
