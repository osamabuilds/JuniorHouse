import { ChangeDetectionStrategy, Component, OnInit, inject, input, output, signal } from '@angular/core';
import { AbstractControl, FormBuilder, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { ApiError } from '@core/http';
import { ControlErrorsComponent, FieldErrorsComponent } from '@shared';
import { GRADES, REASON_DIRECTION } from '../../adjustments.constants';
import { AdjustmentReasonDto, PendingReconfirm, PostAdjustmentValue } from '../../models';

/** A non-zero whole number: a stock change is a signed quantity, never zero (ADJ-7). */
function nonZeroInteger(control: AbstractControl): ValidationErrors | null {
  const value = Number(control.value);
  return Number.isInteger(value) && value !== 0 && Math.abs(value) <= 100_000 ? null : { nonZero: true };
}

/**
 * Records a stock adjustment (ADJ-0..9): the barcode, the bin, the signed change, and a reason, which is required. Above the
 * large-adjustment limit the server refuses the first send; this form then shows a clear confirmation step and, if the person
 * confirms, resends the very same attempt with the same idempotency key (ADJ-4b, ADJ-8). Presentational only.
 */
@Component({
  selector: 'app-adjustment-form',
  imports: [ReactiveFormsModule, ControlErrorsComponent, FieldErrorsComponent],
  template: `
    <form class="card" [formGroup]="form" (ngSubmit)="submit()" aria-labelledby="af-heading" novalidate>
      <h2 id="af-heading">New adjustment</h2>
      <p class="field-hint">
        Use this to correct the system when the shelf and the books disagree. Every adjustment keeps its reason and your name, and cannot
        be edited later: a mistake is fixed with another adjustment.
      </p>

      @if (reconfirm(); as pending) {
        <div class="banner banner--error" role="alertdialog" aria-labelledby="af-confirm-heading" aria-describedby="af-confirm-text">
          <h3 id="af-confirm-heading">Please confirm this change</h3>
          <p id="af-confirm-text">{{ pending.message }}</p>
          <div class="form-actions">
            <button type="button" class="button button--danger" [disabled]="saving()" (click)="confirmed.emit(pending.value)">Yes, apply this change</button>
            <button type="button" class="button" (click)="declined.emit()">Go back and change it</button>
          </div>
        </div>
      }

      @if (error()) {
        <p class="banner banner--error" role="alert">{{ error()?.message }}</p>
      }
      @if (summary()) {
        <p class="banner banner--error" role="alert">{{ summary() }}</p>
      }

      <div class="form-grid">
        <div class="form-field">
          <label for="af-gtin">Barcode (13 digits)</label>
          <input id="af-gtin" type="text" inputmode="numeric" formControlName="gtin" maxlength="13" autocomplete="off" />
          <app-control-errors [control]="form.controls.gtin" label="Barcode" />
          <app-field-errors [messages]="fieldErrors('Gtin')" />
        </div>
        <div class="form-field">
          <label for="af-bin">Bin</label>
          <input id="af-bin" type="text" formControlName="binCode" maxlength="12" autocomplete="off" placeholder="For example A-03-2" />
          <app-control-errors [control]="form.controls.binCode" label="Bin" />
          <app-field-errors [messages]="fieldErrors('BinCode')" />
        </div>
        <div class="form-field">
          <label for="af-grade">Grade</label>
          <select id="af-grade" formControlName="gradeId">
            @for (grade of grades; track grade.id) {
              <option [ngValue]="grade.id">{{ grade.label }}</option>
            }
          </select>
        </div>
        <div class="form-field">
          <label for="af-reason">Reason</label>
          <select id="af-reason" formControlName="reasonId">
            <option [ngValue]="0">Choose a reason…</option>
            @for (reason of reasons(); track reason.id) {
              <option [ngValue]="reason.id">{{ reason.name }} ({{ direction(reason.code) }})</option>
            }
          </select>
          <app-control-errors [control]="form.controls.reasonId" label="Reason" />
          <app-field-errors [messages]="fieldErrors('ReasonId')" />
        </div>
        <div class="form-field">
          <label for="af-change">Change (use a minus to remove)</label>
          <input id="af-change" type="number" inputmode="numeric" formControlName="quantityChange" step="1" />
          <app-control-errors [control]="form.controls.quantityChange" label="Change" />
          <app-field-errors [messages]="fieldErrors('QuantityChange')" />
        </div>
        <div class="form-field">
          <label for="af-person">Your name</label>
          <input id="af-person" type="text" formControlName="actingPerson" maxlength="100" autocomplete="name" />
          <app-control-errors [control]="form.controls.actingPerson" label="Your name" />
          <app-field-errors [messages]="fieldErrors('ActingPerson')" />
        </div>
        <div class="form-field form-field--wide">
          <label for="af-note">Note (optional)</label>
          <input id="af-note" type="text" formControlName="note" maxlength="200" />
          <app-field-errors [messages]="fieldErrors('Note')" />
        </div>
      </div>

      <div class="form-actions">
        <button type="submit" class="button button--primary" [disabled]="saving()">{{ saving() ? 'Saving…' : 'Record adjustment' }}</button>
        <button type="button" class="button" (click)="cancelled.emit()">Cancel</button>
      </div>
    </form>
  `,
  styles: ':host { display: block; margin-bottom: var(--space-5); }',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AdjustmentFormComponent implements OnInit {
  private readonly formBuilder = inject(FormBuilder);

  readonly reasons = input.required<readonly AdjustmentReasonDto[]>();
  readonly saving = input(false);
  readonly error = input<ApiError | null>(null);
  readonly reconfirm = input<PendingReconfirm | null>(null);
  readonly defaultPerson = input('');

  readonly submitted = output<PostAdjustmentValue>();
  readonly confirmed = output<PostAdjustmentValue>();
  readonly declined = output<void>();
  readonly cancelled = output<void>();

  readonly grades = GRADES;
  readonly summary = signal<string | null>(null);

  /** One key per attempt: sending the same attempt twice (a double tap, the confirmation step) applies once (ADJ-8). */
  private readonly idempotencyKey = crypto.randomUUID();

  readonly form = this.formBuilder.group({
    gtin: this.formBuilder.nonNullable.control('', [Validators.required, Validators.pattern(/^[0-9]{13}$/)]),
    binCode: this.formBuilder.nonNullable.control('', [Validators.required, Validators.maxLength(12)]),
    gradeId: this.formBuilder.nonNullable.control(1),
    reasonId: this.formBuilder.nonNullable.control(0, [Validators.min(1)]),
    quantityChange: this.formBuilder.nonNullable.control(0, [nonZeroInteger]),
    note: this.formBuilder.nonNullable.control('', Validators.maxLength(200)),
    actingPerson: this.formBuilder.nonNullable.control('', [Validators.required, Validators.minLength(2), Validators.maxLength(100)]),
  });

  ngOnInit(): void {
    this.form.patchValue({ actingPerson: this.defaultPerson() });
  }

  direction(code: string): string {
    return REASON_DIRECTION[code] ?? '';
  }

  fieldErrors(field: string): readonly string[] {
    return this.error()?.fieldErrors[field] ?? [];
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.summary.set("This can't be saved yet. Check the barcode, bin, reason, the change (not zero) and your name.");
      return;
    }

    this.summary.set(null);
    const value = this.form.getRawValue();
    this.submitted.emit({
      gtin: value.gtin,
      binCode: value.binCode.trim().toUpperCase(),
      gradeId: value.gradeId,
      quantityChange: Math.trunc(Number(value.quantityChange)),
      reasonId: value.reasonId,
      note: value.note.trim() || null,
      reconfirmed: false,
      idempotencyKey: this.idempotencyKey,
      actingPerson: value.actingPerson.trim(),
    });
  }
}
