import { ChangeDetectionStrategy, Component, OnInit, inject, input, output, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ApiError } from '@core/http';
import { ControlErrorsComponent, FieldErrorsComponent } from '@shared';
import { ConfirmPutawayValue, PutawaySelection } from '../../models';

/**
 * Confirms putting an item away (PUT-4): which bin, how many. The quantity starts at everything still pending and cannot go above it.
 * A refusal from the server (wrong bin, inactive bin, too many units) is shown in its own words and nothing typed is lost. The
 * idempotency key travels with the selection so a double tap applies once (PUT-8). Presentational only.
 */
@Component({
  selector: 'app-putaway-confirm-form',
  imports: [ReactiveFormsModule, ControlErrorsComponent, FieldErrorsComponent],
  template: `
    <form class="card" [formGroup]="form" (ngSubmit)="submit()" aria-labelledby="pc-heading" novalidate>
      <h2 id="pc-heading">Put away {{ selection().label }}</h2>
      <p class="field-hint">
        {{ selection().pending }} unit(s) are waiting ({{ selection().gradeCode === 'CONCESSION' ? 'concession' : 'accepted' }}).
        @if (selection().scanned) {
          The barcode was scanned.
        } @else {
          Chosen from the list, because there is no tag to scan yet.
        }
      </p>

      @if (error()) {
        <p class="banner banner--error" role="alert">{{ error()?.message }}</p>
      }
      @if (summary()) {
        <p class="banner banner--error" role="alert">{{ summary() }}</p>
      }

      <div class="form-grid">
        <div class="form-field">
          <label for="pc-bin">Bin</label>
          <input id="pc-bin" type="text" formControlName="binCode" maxlength="12" autocomplete="off" placeholder="Scan or type the code on the shelf" />
          <app-control-errors [control]="form.controls.binCode" label="Bin" />
          <app-field-errors [messages]="fieldErrors('BinCode')" />
        </div>
        <div class="form-field">
          <label for="pc-qty">How many</label>
          <input id="pc-qty" type="number" inputmode="numeric" formControlName="quantity" min="1" [max]="selection().pending" />
          <app-control-errors [control]="form.controls.quantity" label="Quantity" />
          <app-field-errors [messages]="fieldErrors('Quantity')" />
        </div>
        <div class="form-field">
          <label for="pc-person">Your name</label>
          <input id="pc-person" type="text" formControlName="actingPerson" maxlength="100" autocomplete="name" />
          <app-control-errors [control]="form.controls.actingPerson" label="Your name" />
          <app-field-errors [messages]="fieldErrors('ActingPerson')" />
        </div>
      </div>

      <div class="form-actions">
        <button type="submit" class="button button--primary" [disabled]="saving()">{{ saving() ? 'Putting away…' : 'Put away' }}</button>
        <button type="button" class="button" (click)="cancelled.emit()">Cancel</button>
      </div>
    </form>
  `,
  styles: ':host { display: block; margin-bottom: var(--space-5); }',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PutawayConfirmFormComponent implements OnInit {
  private readonly formBuilder = inject(FormBuilder);

  readonly selection = input.required<PutawaySelection>();
  readonly saving = input(false);
  readonly error = input<ApiError | null>(null);
  readonly defaultPerson = input('');

  readonly confirmed = output<ConfirmPutawayValue>();
  readonly cancelled = output<void>();

  readonly summary = signal<string | null>(null);

  readonly form = this.formBuilder.group({
    binCode: this.formBuilder.nonNullable.control('', [Validators.required, Validators.maxLength(12)]),
    quantity: this.formBuilder.nonNullable.control(1, [Validators.required, Validators.min(1)]),
    actingPerson: this.formBuilder.nonNullable.control('', [Validators.required, Validators.minLength(2), Validators.maxLength(100)]),
  });

  ngOnInit(): void {
    this.form.controls.quantity.addValidators(Validators.max(this.selection().pending));
    this.form.patchValue({ quantity: this.selection().pending, actingPerson: this.defaultPerson() });
  }

  fieldErrors(field: string): readonly string[] {
    return this.error()?.fieldErrors[field] ?? [];
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.summary.set("This can't be saved yet. Check the bin, the quantity (at most what is pending) and your name.");
      return;
    }

    this.summary.set(null);
    const value = this.form.getRawValue();
    const selection = this.selection();
    this.confirmed.emit({
      binCode: value.binCode.trim().toUpperCase(),
      quantity: Math.trunc(value.quantity),
      scanned: selection.scanned,
      gtin: selection.scanned ? selection.gtin : null,
      idempotencyKey: selection.idempotencyKey,
      actingPerson: value.actingPerson.trim(),
    });
  }
}
