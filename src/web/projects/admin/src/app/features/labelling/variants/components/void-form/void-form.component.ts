import { ChangeDetectionStrategy, Component, OnInit, inject, input, output, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ApiError } from '@core/http';
import { ControlErrorsComponent, FieldErrorsComponent } from '@shared';
import { VariantRowDto, VoidGtinValue } from '../../models';
import { VOID_REASONS } from '../../variants.constants';

/**
 * Voids a SKU's barcode number (VAR-8, D8). It can be done at any time with a reason and the person doing it. The number is retired
 * for good and never issued again; tags already on garments are not touched; the SKU gets a new number with its next accepted stock.
 */
@Component({
  selector: 'app-void-form',
  imports: [ReactiveFormsModule, ControlErrorsComponent, FieldErrorsComponent],
  template: `
    <form class="card" [formGroup]="form" (ngSubmit)="submit()" aria-labelledby="vf-heading" novalidate>
      <h2 id="vf-heading">Void the barcode for {{ variant().skuCode }}</h2>
      <p class="field-hint">
        {{ variant().activeGtin }} will be retired for good and never used again, so no tag can be printed for it. Tags already on garments
        are not changed. The SKU gets a new number when more stock for it is accepted.
      </p>

      @if (error()) {
        <p class="banner banner--error" role="alert">{{ error()?.message }}</p>
      }
      @if (summary()) {
        <p class="banner banner--error" role="alert">{{ summary() }}</p>
      }

      <div class="form-grid">
        <div class="form-field">
          <label for="vf-reason">Why</label>
          <select id="vf-reason" formControlName="reasonId">
            @for (reason of reasons; track reason.id) {
              <option [ngValue]="reason.id">{{ reason.label }}</option>
            }
          </select>
          <app-field-errors [messages]="fieldErrors('ReasonId')" />
        </div>
        <div class="form-field">
          <label for="vf-person">Your name</label>
          <input id="vf-person" type="text" formControlName="actingPerson" maxlength="100" autocomplete="name" />
          <app-control-errors [control]="form.controls.actingPerson" label="Your name" />
          <app-field-errors [messages]="fieldErrors('ActingPerson')" />
        </div>
        <div class="form-field form-field--wide">
          <label for="vf-note">Note (optional)</label>
          <input id="vf-note" type="text" formControlName="note" maxlength="200" />
          <app-field-errors [messages]="fieldErrors('Note')" />
        </div>
      </div>

      <div class="form-actions">
        <button type="submit" class="button button--danger" [disabled]="saving()">{{ saving() ? 'Voiding…' : 'Void this barcode' }}</button>
        <button type="button" class="button" (click)="cancelled.emit()">Cancel</button>
      </div>
    </form>
  `,
  styles: ':host { display: block; margin-bottom: var(--space-5); }',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class VoidFormComponent implements OnInit {
  private readonly formBuilder = inject(FormBuilder);

  readonly variant = input.required<VariantRowDto>();
  readonly saving = input(false);
  readonly error = input<ApiError | null>(null);
  readonly defaultPerson = input('');

  readonly submitted = output<VoidGtinValue>();
  readonly cancelled = output<void>();

  readonly reasons = VOID_REASONS;
  readonly summary = signal<string | null>(null);

  readonly form = this.formBuilder.group({
    reasonId: this.formBuilder.nonNullable.control(VOID_REASONS[0].id),
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
      this.summary.set("This can't be saved yet. Check your name.");
      return;
    }

    this.summary.set(null);
    const value = this.form.getRawValue();
    this.submitted.emit({ reasonId: value.reasonId, note: value.note.trim() || null, actingPerson: value.actingPerson.trim() });
  }
}
