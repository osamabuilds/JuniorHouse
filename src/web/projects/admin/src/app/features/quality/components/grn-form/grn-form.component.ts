import { ChangeDetectionStrategy, Component, OnInit, computed, inject, input, output, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ApiError } from '@core/http';
import { LookupNameResolver } from '@features/reference-data';
import { ControlErrorsComponent, FieldErrorsComponent, inputNumber, missingSummary } from '@shared';
import { InboundDeliveryDto, RecordReceiptValue } from '../../models';
import { lineKey, previewReconciliation, todayInPakistan } from '../../utils';
import { ReconciliationBadgeComponent } from '../reconciliation-badge/reconciliation-badge.component';

/**
 * Records what was actually counted against a vendor delivery note (BRD 5.11, spec AC-3, AC-6, AC-7, AC-8, AC-11): the counted
 * quantity per size and colour next to what the vendor claimed, the day it arrived and who is recording it. The reconciliation is
 * shown as it is typed, but the API's answer is the one that is stored. A shortage or an excess never blocks the receipt.
 */
@Component({
  selector: 'app-grn-form',
  imports: [ReactiveFormsModule, ControlErrorsComponent, FieldErrorsComponent, ReconciliationBadgeComponent],
  template: `
    <form class="card" [formGroup]="form" (ngSubmit)="submit()" aria-labelledby="grn-heading" novalidate>
      <h2 id="grn-heading">Count the goods</h2>
      <p class="field-hint">Count what actually arrived. Enter 0 for anything that did not come. Differences from the delivery note are recorded, not refused.</p>

      @if (error()) {
        <p class="banner banner--error" role="alert">{{ error()?.message }}</p>
      }
      @if (summary()) {
        <p class="banner banner--error" role="alert">{{ summary() }}</p>
      }

      <table class="responsive line-grid">
        <caption class="sr-only">
          Counted quantities
        </caption>
        <thead>
          <tr>
            <th scope="col">Size</th>
            <th scope="col">Colour</th>
            <th scope="col">Vendor says</th>
            <th scope="col">Counted</th>
            <th scope="col">Difference</th>
          </tr>
        </thead>
        <tbody>
          @for (line of delivery().lines; track lineId(line.sizeId, line.colourId)) {
            <tr>
              <td data-label="Size">{{ names().sizeName(line.sizeId) }}</td>
              <td data-label="Colour">{{ names().colourName(line.colourId) }}</td>
              <td data-label="Vendor says">{{ line.claimedQty }}</td>
              <td data-label="Counted">
                <label class="sr-only" [for]="'grn-' + lineId(line.sizeId, line.colourId)">
                  Counted {{ names().sizeName(line.sizeId) }} {{ names().colourName(line.colourId) }}
                </label>
                <input
                  [id]="'grn-' + lineId(line.sizeId, line.colourId)"
                  type="number"
                  min="0"
                  step="1"
                  [value]="counted()[lineId(line.sizeId, line.colourId)]"
                  (input)="onCounted(line.sizeId, line.colourId, $event)"
                />
              </td>
              <td data-label="Difference">{{ diff(line.sizeId, line.colourId, line.claimedQty) }}</td>
            </tr>
          }
        </tbody>
      </table>

      <p class="field-hint" aria-live="polite">
        Total: {{ preview().countedTotal }} counted of {{ preview().claimedTotal }} claimed.
        <app-reconciliation-badge [code]="preview().code" [diff]="preview().diffTotal" />
      </p>
      <app-field-errors [messages]="fieldErrors('Lines')" />

      <div class="form-grid">
        <div class="form-field">
          <label for="grn-date">Date received</label>
          <input id="grn-date" type="date" formControlName="receivedDate" [min]="delivery().dispatchDate" [max]="today" />
          <p class="field-hint">From the dispatch date ({{ delivery().dispatchDate }}) to today.</p>
          <app-control-errors [control]="form.controls.receivedDate" label="Date received" />
          <app-field-errors [messages]="fieldErrors('ReceivedDate')" />
        </div>

        <div class="form-field">
          <label for="grn-person">Your name</label>
          <input id="grn-person" type="text" formControlName="actingPerson" maxlength="100" autocomplete="name" />
          <app-control-errors [control]="form.controls.actingPerson" label="Your name" />
          <app-field-errors [messages]="fieldErrors('ActingPerson')" />
        </div>

        <div class="form-field form-field--wide">
          <label for="grn-note">Note (optional)</label>
          <textarea id="grn-note" rows="2" formControlName="note" maxlength="500"></textarea>
          <app-field-errors [messages]="fieldErrors('Note')" />
        </div>
      </div>

      <div class="form-actions">
        <button type="submit" class="button button--primary" [disabled]="saving()">{{ saving() ? 'Saving…' : 'Record goods receipt' }}</button>
      </div>
    </form>
  `,
  styles: ':host { display: block; margin-bottom: var(--space-5); }',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class GrnFormComponent implements OnInit {
  private readonly formBuilder = inject(FormBuilder);

  readonly delivery = input.required<InboundDeliveryDto>();
  readonly names = input.required<LookupNameResolver>();
  readonly saving = input(false);
  readonly error = input<ApiError | null>(null);
  /** The name last used in this browser, so the field starts filled. */
  readonly defaultPerson = input('');

  readonly submitted = output<RecordReceiptValue>();

  readonly today = todayInPakistan();
  readonly summary = signal<string | null>(null);
  readonly counted = signal<Readonly<Record<string, number>>>({});

  readonly form = this.formBuilder.group({
    receivedDate: this.formBuilder.nonNullable.control(this.today, Validators.required),
    actingPerson: this.formBuilder.nonNullable.control('', [Validators.required, Validators.minLength(2), Validators.maxLength(100)]),
    note: this.formBuilder.nonNullable.control('', Validators.maxLength(500)),
  });

  readonly preview = computed(() =>
    previewReconciliation(
      this.delivery().lines.map((line) => ({ claimedQty: line.claimedQty, countedQty: this.counted()[lineKey(line.sizeId, line.colourId)] ?? 0 })),
    ),
  );

  ngOnInit(): void {
    this.form.patchValue({ actingPerson: this.defaultPerson() });
    // Start from what the vendor claims: the usual case is that it all arrived.
    this.counted.set(Object.fromEntries(this.delivery().lines.map((line) => [lineKey(line.sizeId, line.colourId), line.claimedQty])));
  }

  lineId(sizeId: number, colourId: number): string {
    return lineKey(sizeId, colourId);
  }

  diff(sizeId: number, colourId: number, claimed: number): string {
    const difference = (this.counted()[lineKey(sizeId, colourId)] ?? 0) - claimed;
    if (difference === 0) {
      return 'Matches';
    }
    return difference < 0 ? `Short by ${-difference}` : `Over by ${difference}`;
  }

  fieldErrors(field: string): readonly string[] {
    return this.error()?.fieldErrors[field] ?? [];
  }

  onCounted(sizeId: number, colourId: number, event: Event): void {
    this.counted.set({ ...this.counted(), [lineKey(sizeId, colourId)]: Math.max(0, Math.trunc(inputNumber(event)) || 0) });
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.summary.set(
        missingSummary(this.form.controls, { receivedDate: 'Date received', actingPerson: 'Your name' }),
      );
      return;
    }

    this.summary.set(null);
    const value = this.form.getRawValue();
    this.submitted.emit({
      inboundDeliveryId: this.delivery().id,
      receivedDate: value.receivedDate,
      actingPerson: value.actingPerson.trim(),
      note: value.note.trim() || null,
      lines: this.delivery().lines.map((line) => ({
        sizeId: line.sizeId,
        colourId: line.colourId,
        qty: this.counted()[lineKey(line.sizeId, line.colourId)] ?? 0,
      })),
    });
  }
}
