import { Component, computed, input, output, signal } from '@angular/core';
import { inputChecked } from '@shared';
import { PoDto } from './po-api.service';

interface ChecklistItem {
  readonly label: string;
  readonly done: boolean;
}

/**
 * SCRUM-93 task 53 (AC-39): the confirmation step before Send. The checklist is a reminder, not a
 * gate - except that sending without a tech pack spec needs an explicit "send anyway", which the API
 * also enforces and records in the PO's status history.
 */
@Component({
  selector: 'app-send-confirm',
  template: `
    <section class="send card" role="alertdialog" aria-labelledby="send-heading" aria-describedby="send-desc">
      <h3 id="send-heading">Send {{ po().poNo }} to the vendor?</h3>
      <p id="send-desc">Once sent, terms change only through an amendment. Before you send, check:</p>

      <ul class="send__checklist">
        @for (item of checklist(); track item.label) {
          <li [class.send__done]="item.done">
            <span aria-hidden="true">{{ item.done ? '✓' : '○' }}</span>
            {{ item.label }}
            <span class="sr-only">{{ item.done ? '(done)' : '(not done)' }}</span>
          </li>
        }
      </ul>

      @if (!hasTechPack()) {
        <p class="banner banner--error" role="note">
          No Tech Pack Spec is attached. The vendor will not have the specification. Attach one in Files, or
          confirm you want to send without it.
        </p>
        <label class="send__anyway">
          <input type="checkbox" [checked]="sendAnyway()" (change)="onAnyway($event)" />
          Send anyway, without a tech pack
        </label>
      }

      <div class="form-actions">
        <button type="button" class="button button--primary" [disabled]="!canConfirm()" (click)="confirmed.emit(sendAnyway())">
          Send to Vendor
        </button>
        <button type="button" class="button" (click)="dismissed.emit()">Not yet</button>
      </div>
    </section>
  `,
  styles: `
    .send {
      margin-bottom: var(--space-5);
      border-left: 4px solid var(--color-brass);
    }
    .send__checklist {
      list-style: none;
      padding: 0;
      margin: 0 0 var(--space-4);
      display: grid;
      gap: var(--space-1);
    }
    .send__done {
      color: var(--color-success);
    }
    .send__anyway {
      display: flex;
      align-items: center;
      gap: var(--space-2);
      margin-bottom: var(--space-3);
    }
  `,
})
export class SendConfirm {
  readonly po = input.required<PoDto>();
  readonly hasTechPack = input.required<boolean>();

  readonly confirmed = output<boolean>();
  readonly dismissed = output<void>();

  readonly sendAnyway = signal(false);

  readonly checklist = computed<ChecklistItem[]>(() => {
    const po = this.po();
    return [
      { label: 'Tech pack spec attached', done: this.hasTechPack() },
      { label: 'Latest acceptable delivery date set', done: po.latestAcceptableDate !== null },
      { label: 'Fabric responsibility set', done: po.fabricResponsibilityId !== null },
      { label: 'Unit cost and quantities reviewed', done: po.unitCost > 0 && po.lines.length > 0 },
    ];
  });

  readonly canConfirm = computed(() => this.hasTechPack() || this.sendAnyway());

  onAnyway(event: Event): void {
    this.sendAnyway.set(inputChecked(event));
  }
}
