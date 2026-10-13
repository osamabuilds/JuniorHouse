import { ChangeDetectionStrategy, Component, OnInit, inject, input, output, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ApiError } from '@core/http';
import { ControlErrorsComponent, FieldErrorsComponent } from '@shared';
import { REPRINT_REASONS } from '../../print-jobs.constants';
import { PrinterDto, ReprintValue } from '../../models';

/**
 * Reprints a tag that was lost or damaged (FR-SC-07, PJ-5..PJ-8): the same barcode, with a reason and the person asking. No new
 * GTIN is created, so the SKU's sales history stays continuous. A reprint prints the CURRENT price, which is also how a price
 * change is put right. The printer is chosen from the configured list by code (PJ-12).
 */
@Component({
  selector: 'app-reprint-form',
  imports: [ReactiveFormsModule, ControlErrorsComponent, FieldErrorsComponent],
  template: `
    <form class="card" [formGroup]="form" (ngSubmit)="submit()" aria-labelledby="rp-heading" novalidate>
      <h2 id="rp-heading">Reprint tags{{ sku() ? ' for ' + sku() : '' }}</h2>
      <p class="field-hint">The barcode number does not change. The tag shows today's price.</p>

      @if (error()) {
        <p class="banner banner--error" role="alert">{{ error()?.message }}</p>
      }
      @if (summary()) {
        <p class="banner banner--error" role="alert">{{ summary() }}</p>
      }

      <div class="form-grid">
        <div class="form-field">
          <label for="rp-qty">How many tags</label>
          <input id="rp-qty" type="number" inputmode="numeric" min="1" max="5000" step="1" formControlName="qty" />
          <app-control-errors [control]="form.controls.qty" label="How many tags" />
          <app-field-errors [messages]="fieldErrors('Qty')" />
        </div>
        <div class="form-field">
          <label for="rp-reason">Why</label>
          <select id="rp-reason" formControlName="reasonId">
            @for (reason of reasons; track reason.id) {
              <option [ngValue]="reason.id">{{ reason.label }}</option>
            }
          </select>
          <app-field-errors [messages]="fieldErrors('ReasonId')" />
        </div>
        <div class="form-field">
          <label for="rp-printer">Printer</label>
          <select id="rp-printer" formControlName="printerCode">
            <option value="">Default printer</option>
            @for (printer of printers(); track printer.id) {
              <option [value]="printer.code">{{ printer.name }}</option>
            }
          </select>
          <app-field-errors [messages]="fieldErrors('PrinterCode')" />
        </div>
        <div class="form-field">
          <label for="rp-person">Your name</label>
          <input id="rp-person" type="text" formControlName="actingPerson" maxlength="100" autocomplete="name" />
          <app-control-errors [control]="form.controls.actingPerson" label="Your name" />
          <app-field-errors [messages]="fieldErrors('ActingPerson')" />
        </div>
        <div class="form-field form-field--wide">
          <label for="rp-note">Note (optional)</label>
          <input id="rp-note" type="text" formControlName="note" maxlength="200" />
          <app-field-errors [messages]="fieldErrors('Note')" />
        </div>
      </div>

      <div class="form-actions">
        <button type="submit" class="button button--primary" [disabled]="saving()">{{ saving() ? 'Queueing…' : 'Queue reprint' }}</button>
        <button type="button" class="button" (click)="cancelled.emit()">Cancel</button>
      </div>
    </form>
  `,
  styles: ':host { display: block; margin-bottom: var(--space-5); }',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ReprintFormComponent implements OnInit {
  private readonly formBuilder = inject(FormBuilder);

  readonly sku = input<string | null>(null);
  readonly printers = input.required<readonly PrinterDto[]>();
  readonly saving = input(false);
  readonly error = input<ApiError | null>(null);
  readonly defaultPerson = input('');

  readonly submitted = output<ReprintValue>();
  readonly cancelled = output<void>();

  readonly reasons = REPRINT_REASONS;
  readonly summary = signal<string | null>(null);

  readonly form = this.formBuilder.group({
    qty: this.formBuilder.nonNullable.control(1, [Validators.required, Validators.min(1), Validators.max(5000)]),
    reasonId: this.formBuilder.nonNullable.control(REPRINT_REASONS[0].id),
    printerCode: this.formBuilder.nonNullable.control(''),
    note: this.formBuilder.nonNullable.control('', Validators.maxLength(200)),
    actingPerson: this.formBuilder.nonNullable.control('', [Validators.required, Validators.minLength(2), Validators.maxLength(100)]),
  });

  ngOnInit(): void {
    this.form.patchValue({ actingPerson: this.defaultPerson() });
  }

  fieldErrors(field: string): readonly string[] {
    return this.error()?.fieldErrors[field] ?? [];
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.summary.set("This can't be queued yet. Check the number of tags and your name.");
      return;
    }

    this.summary.set(null);
    const value = this.form.getRawValue();
    this.submitted.emit({
      qty: value.qty,
      reasonId: value.reasonId,
      note: value.note.trim() || null,
      printerCode: value.printerCode || null,
      actingPerson: value.actingPerson.trim(),
    });
  }
}
