import { Component, computed, input, output, signal } from '@angular/core';
import { inputValue } from '@shared';
import { PoRevisionDto } from '../../models';
import { INITIATOR_LABELS } from '../../purchase-orders.constants';

/**
 * SCRUM-93 task 50 (AC-11..AC-13): Accept / Reject / Withdraw for the PO's one open Pending
 * revision. Rendered by the parent only when a Pending revision exists. It names who decides: a
 * buyer's proposal is decided by the vendor (staff record their answer here), a vendor's by Romp.
 * Only the proposer can withdraw.
 */
@Component({
  selector: 'app-pending-revision-actions',
  template: `
    <section class="pending card" aria-labelledby="pending-heading">
      <h3 id="pending-heading">Revision {{ revision().revisionNumber }} awaits a decision</h3>
      <p>
        Proposed by <strong>{{ proposer() }}</strong>. The decision is <strong>{{ decider() }}</strong>'s: PKR
        {{ revision().unitCost }} per piece, delivery {{ revision().expectedDeliveryDate }}.
      </p>

      <label for="pending-note">Note (optional)</label>
      <input id="pending-note" type="text" [value]="note()" (input)="onNote($event)" />

      <div class="pending__actions">
        <button type="button" class="button button--primary" (click)="accepted.emit()">
          Accept (on {{ decider() }}'s behalf)
        </button>
        <button type="button" class="button" (click)="rejected.emit(noteOrNull())">Reject</button>
        <button type="button" class="button" (click)="withdrawn.emit(noteOrNull())">Withdraw (by {{ proposer() }})</button>
      </div>
    </section>
  `,
  styles: `
    .pending {
      margin-bottom: var(--space-5);
      border-left: 4px solid var(--color-brass);
    }
    .pending__actions {
      display: flex;
      flex-wrap: wrap;
      gap: var(--space-2);
      margin-top: var(--space-3);
    }
  `,
})
export class PendingRevisionActionsComponent {
  readonly revision = input.required<PoRevisionDto>();

  readonly accepted = output<void>();
  readonly rejected = output<string | null>();
  readonly withdrawn = output<string | null>();

  readonly note = signal('');

  readonly proposer = computed(() => INITIATOR_LABELS[this.revision().initiatorId] ?? 'the proposer');
  readonly decider = computed(() => INITIATOR_LABELS[this.revision().initiatorId === 1 ? 2 : 1] ?? 'the other party');

  onNote(event: Event): void {
    this.note.set(inputValue(event));
  }

  noteOrNull(): string | null {
    return this.note().trim() || null;
  }
}
