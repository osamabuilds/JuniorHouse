import { ChangeDetectionStrategy, Component, OnInit, computed, inject, input, output, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ApiError } from '@core/http';
import { LookupNameResolver } from '@features/reference-data';
import { ControlErrorsComponent, FieldErrorsComponent, inputNumber } from '@shared';
import { ReworkReturnValue, SubBatchDto } from '../../models';
import { lineKey, todayInPakistan } from '../../utils';

/**
 * Records the vendor sending reworked goods back (spec AC-26, AC-27): how many of each size and colour, never more than is still
 * outstanding. What does not come back stays visible as outstanding on the rework part; it is neither accepted nor rejected. The
 * returned pieces become a new part that must be re-inspected, with both safety checks recorded again, before it can be accepted.
 */
@Component({
  selector: 'app-rework-return-form',
  imports: [ReactiveFormsModule, ControlErrorsComponent, FieldErrorsComponent],
  template: `
    <form class="card" [formGroup]="form" (ngSubmit)="submit()" aria-labelledby="return-heading" novalidate>
      <h3 id="return-heading">Record goods back from the vendor</h3>
      <p class="field-hint">
        Enter how many of each came back. You can record a partial return now and the rest later: what has not come back stays outstanding.
      </p>

      @if (error()) {
        <p class="banner banner--error" role="alert">{{ error()?.message }}</p>
      }
      @if (summary()) {
        <p class="banner banner--error" role="alert">{{ summary() }}</p>
      }

      <table class="responsive line-grid">
        <caption class="sr-only">
          Returned quantities
        </caption>
        <thead>
          <tr>
            <th scope="col">Size</th>
            <th scope="col">Colour</th>
            <th scope="col">Still with the vendor</th>
            <th scope="col">Came back</th>
          </tr>
        </thead>
        <tbody>
          @for (line of openLines(); track lineId(line.sizeId, line.colourId)) {
            <tr>
              <td data-label="Size">{{ names().sizeName(line.sizeId) }}</td>
              <td data-label="Colour">{{ names().colourName(line.colourId) }}</td>
              <td data-label="Still with the vendor">{{ line.outstandingQty }}</td>
              <td data-label="Came back">
                <label class="sr-only" [for]="'rt-' + lineId(line.sizeId, line.colourId)">
                  Came back {{ names().sizeName(line.sizeId) }} {{ names().colourName(line.colourId) }}
                </label>
                <input
                  [id]="'rt-' + lineId(line.sizeId, line.colourId)"
                  type="number"
                  min="0"
                  [max]="line.outstandingQty"
                  step="1"
                  [value]="returned()[lineId(line.sizeId, line.colourId)] ?? 0"
                  (input)="onReturned(line.sizeId, line.colourId, $event)"
                />
              </td>
            </tr>
          }
        </tbody>
      </table>
      <app-field-errors [messages]="fieldErrors('Lines')" />

      <div class="form-grid">
        <div class="form-field">
          <label for="rt-date">Date returned</label>
          <input id="rt-date" type="date" formControlName="returnDate" [max]="today" />
          <app-control-errors [control]="form.controls.returnDate" label="Date returned" />
          <app-field-errors [messages]="fieldErrors('ReturnDate')" />
        </div>
        <div class="form-field">
          <label for="rt-person">Your name</label>
          <input id="rt-person" type="text" formControlName="actingPerson" maxlength="100" autocomplete="name" />
          <app-control-errors [control]="form.controls.actingPerson" label="Your name" />
          <app-field-errors [messages]="fieldErrors('ActingPerson')" />
        </div>
        <div class="form-field form-field--wide">
          <label for="rt-note">Note (optional)</label>
          <textarea id="rt-note" rows="2" formControlName="note" maxlength="500"></textarea>
          <app-field-errors [messages]="fieldErrors('Note')" />
        </div>
      </div>

      <div class="form-actions">
        <button type="submit" class="button button--primary" [disabled]="saving()">{{ saving() ? 'Saving…' : 'Record return' }}</button>
        <button type="button" class="button" (click)="cancelled.emit()">Cancel</button>
      </div>
    </form>
  `,
  styles: ':host { display: block; margin-bottom: var(--space-5); }',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ReworkReturnFormComponent implements OnInit {
  private readonly formBuilder = inject(FormBuilder);

  readonly part = input.required<SubBatchDto>();
  readonly names = input.required<LookupNameResolver>();
  readonly saving = input(false);
  readonly error = input<ApiError | null>(null);
  readonly defaultPerson = input('');

  readonly submitted = output<ReworkReturnValue>();
  readonly cancelled = output<void>();

  readonly today = todayInPakistan();
  readonly summary = signal<string | null>(null);
  readonly returned = signal<Readonly<Record<string, number>>>({});
  readonly openLines = computed(() => this.part().lines.filter((l) => l.outstandingQty > 0));

  readonly form = this.formBuilder.group({
    returnDate: this.formBuilder.nonNullable.control(this.today, Validators.required),
    actingPerson: this.formBuilder.nonNullable.control('', [Validators.required, Validators.minLength(2), Validators.maxLength(100)]),
    note: this.formBuilder.nonNullable.control('', Validators.maxLength(500)),
  });

  ngOnInit(): void {
    this.form.patchValue({ actingPerson: this.defaultPerson() });
  }

  lineId(sizeId: number, colourId: number): string {
    return lineKey(sizeId, colourId);
  }

  fieldErrors(field: string): readonly string[] {
    return this.error()?.fieldErrors[field] ?? [];
  }

  onReturned(sizeId: number, colourId: number, event: Event): void {
    this.returned.set({ ...this.returned(), [lineKey(sizeId, colourId)]: Math.max(0, Math.trunc(inputNumber(event)) || 0) });
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.summary.set("This can't be saved yet. Still needed: your name.");
      return;
    }

    const lines = this.openLines()
      .map((l) => ({ sizeId: l.sizeId, colourId: l.colourId, qty: this.returned()[lineKey(l.sizeId, l.colourId)] ?? 0 }))
      .filter((l) => l.qty > 0);
    if (lines.length === 0) {
      this.summary.set('Enter how many came back for at least one size and colour.');
      return;
    }

    const tooMany = this.openLines().find((l) => (this.returned()[lineKey(l.sizeId, l.colourId)] ?? 0) > l.outstandingQty);
    if (tooMany) {
      this.summary.set(
        `${this.names().sizeName(tooMany.sizeId)} / ${this.names().colourName(tooMany.colourId)}: only ${tooMany.outstandingQty} are still with the vendor.`,
      );
      return;
    }

    this.summary.set(null);
    const value = this.form.getRawValue();
    this.submitted.emit({
      subBatchId: this.part().id,
      returnDate: value.returnDate,
      actingPerson: value.actingPerson.trim(),
      note: value.note.trim() || null,
      lines,
    });
  }
}
