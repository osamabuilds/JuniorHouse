import { ChangeDetectionStrategy, Component, OnInit, inject, input, output, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ApiError } from '@core/http';
import { ControlErrorsComponent, FieldErrorsComponent } from '@shared';
import { LabelWorkItemDto, PrinterDto, QueueTagsValue } from '../../models';

/**
 * Queues tags for one work item (PJ-1). Saving never contacts the printer: the job goes into the queue and prints in the
 * background, so a jam cannot make this form fail. The printer is chosen from the configured list by code, never typed (PJ-12).
 */
@Component({
  selector: 'app-queue-tags-form',
  imports: [ReactiveFormsModule, ControlErrorsComponent, FieldErrorsComponent],
  template: `
    <form class="card" [formGroup]="form" (ngSubmit)="submit()" aria-labelledby="qt-heading" novalidate>
      <h2 id="qt-heading">Queue tags for {{ item().skuCode }}</h2>
      <p class="field-hint">
        {{ item().colourName }} / {{ item().sizeName }}. {{ item().remainingQty }} more tag{{ item().remainingQty === 1 ? ' is' : 's are' }} allowed
        ({{ item().qty }} accepted).
      </p>

      @if (error()) {
        <p class="banner banner--error" role="alert">{{ error()?.message }}</p>
      }
      @if (summary()) {
        <p class="banner banner--error" role="alert">{{ summary() }}</p>
      }

      <div class="form-grid">
        <div class="form-field">
          <label for="qt-qty">How many tags</label>
          <input id="qt-qty" type="number" inputmode="numeric" min="1" [max]="item().remainingQty" step="1" formControlName="qty" />
          <app-control-errors [control]="form.controls.qty" label="How many tags" />
          <app-field-errors [messages]="fieldErrors('Qty')" />
        </div>
        <div class="form-field">
          <label for="qt-printer">Printer</label>
          <select id="qt-printer" formControlName="printerCode">
            <option value="">Default printer</option>
            @for (printer of printers(); track printer.id) {
              <option [value]="printer.code">{{ printer.name }}</option>
            }
          </select>
          <app-field-errors [messages]="fieldErrors('PrinterCode')" />
        </div>
        <div class="form-field">
          <label for="qt-person">Your name</label>
          <input id="qt-person" type="text" formControlName="actingPerson" maxlength="100" autocomplete="name" />
          <app-control-errors [control]="form.controls.actingPerson" label="Your name" />
          <app-field-errors [messages]="fieldErrors('ActingPerson')" />
        </div>
      </div>

      <div class="form-actions">
        <button type="submit" class="button button--primary" [disabled]="saving()">{{ saving() ? 'Queueing…' : 'Queue tags' }}</button>
        <button type="button" class="button" (click)="cancelled.emit()">Cancel</button>
      </div>
    </form>
  `,
  styles: ':host { display: block; margin-bottom: var(--space-5); }',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class QueueTagsFormComponent implements OnInit {
  private readonly formBuilder = inject(FormBuilder);

  readonly item = input.required<LabelWorkItemDto>();
  readonly printers = input.required<readonly PrinterDto[]>();
  readonly saving = input(false);
  readonly error = input<ApiError | null>(null);
  readonly defaultPerson = input('');

  readonly submitted = output<QueueTagsValue>();
  readonly cancelled = output<void>();

  readonly summary = signal<string | null>(null);

  readonly form = this.formBuilder.group({
    qty: this.formBuilder.nonNullable.control(1, [Validators.required, Validators.min(1)]),
    printerCode: this.formBuilder.nonNullable.control(''),
    actingPerson: this.formBuilder.nonNullable.control('', [Validators.required, Validators.minLength(2), Validators.maxLength(100)]),
  });

  ngOnInit(): void {
    this.form.patchValue({ qty: Math.max(1, this.item().remainingQty), actingPerson: this.defaultPerson() });
    this.form.controls.qty.addValidators(Validators.max(Math.max(1, this.item().remainingQty)));
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
    this.submitted.emit({ qty: value.qty, printerCode: value.printerCode || null, actingPerson: value.actingPerson.trim() });
  }
}
