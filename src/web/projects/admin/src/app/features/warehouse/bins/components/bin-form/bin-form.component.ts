import { ChangeDetectionStrategy, Component, OnInit, inject, input, output, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ApiError } from '@core/http';
import { ControlErrorsComponent, FieldErrorsComponent } from '@shared';
import { CreateBinValue } from '../../models';

/**
 * Adds a bin from zone, aisle and shelf (BIN-1). The server builds the code (for example A-03-2) and checks it; the same limits are
 * set on the inputs so most mistakes are caught before sending. Server field errors (BIN-2, BIN-3) are shown under the field.
 */
@Component({
  selector: 'app-bin-form',
  imports: [ReactiveFormsModule, ControlErrorsComponent, FieldErrorsComponent],
  template: `
    <form class="card" [formGroup]="form" (ngSubmit)="submit()" aria-labelledby="bf-heading" novalidate>
      <h2 id="bf-heading">Add a bin</h2>
      <p class="field-hint">
        A bin is a shelf location: zone, aisle and shelf. Together they make the code printed on the shelf, for example A-03-2.
      </p>

      @if (error()) {
        <p class="banner banner--error" role="alert">{{ error()?.message }}</p>
      }
      @if (summary()) {
        <p class="banner banner--error" role="alert">{{ summary() }}</p>
      }

      <div class="form-grid">
        <div class="form-field">
          <label for="bf-zone">Zone</label>
          <input id="bf-zone" type="text" formControlName="zone" maxlength="3" autocomplete="off" />
          <small class="field-hint">1 to 3 letters or digits</small>
          <app-control-errors [control]="form.controls.zone" label="Zone" />
          <app-field-errors [messages]="fieldErrors('Zone')" />
        </div>
        <div class="form-field">
          <label for="bf-aisle">Aisle</label>
          <input id="bf-aisle" type="text" inputmode="numeric" formControlName="aisle" maxlength="3" autocomplete="off" />
          <small class="field-hint">1 to 3 digits</small>
          <app-control-errors [control]="form.controls.aisle" label="Aisle" />
          <app-field-errors [messages]="fieldErrors('Aisle')" />
        </div>
        <div class="form-field">
          <label for="bf-shelf">Shelf</label>
          <input id="bf-shelf" type="text" formControlName="shelf" maxlength="2" autocomplete="off" />
          <small class="field-hint">1 or 2 letters or digits</small>
          <app-control-errors [control]="form.controls.shelf" label="Shelf" />
          <app-field-errors [messages]="fieldErrors('Shelf')" />
        </div>
        <div class="form-field">
          <label for="bf-person">Your name</label>
          <input id="bf-person" type="text" formControlName="actingPerson" maxlength="100" autocomplete="name" />
          <app-control-errors [control]="form.controls.actingPerson" label="Your name" />
          <app-field-errors [messages]="fieldErrors('ActingPerson')" />
        </div>
      </div>

      <div class="form-actions">
        <button type="submit" class="button button--primary" [disabled]="saving()">{{ saving() ? 'Adding…' : 'Add bin' }}</button>
        <button type="button" class="button" (click)="cancelled.emit()">Cancel</button>
      </div>
    </form>
  `,
  styles: ':host { display: block; margin-bottom: var(--space-5); }',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class BinFormComponent implements OnInit {
  private readonly formBuilder = inject(FormBuilder);

  readonly saving = input(false);
  readonly error = input<ApiError | null>(null);
  readonly defaultPerson = input('');

  readonly submitted = output<CreateBinValue>();
  readonly cancelled = output<void>();

  readonly summary = signal<string | null>(null);

  readonly form = this.formBuilder.group({
    zone: this.formBuilder.nonNullable.control('', [Validators.required, Validators.pattern(/^[A-Za-z0-9]{1,3}$/)]),
    aisle: this.formBuilder.nonNullable.control('', [Validators.required, Validators.pattern(/^[0-9]{1,3}$/)]),
    shelf: this.formBuilder.nonNullable.control('', [Validators.required, Validators.pattern(/^[A-Za-z0-9]{1,2}$/)]),
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
      this.summary.set("This can't be saved yet. Check the zone, aisle, shelf and your name.");
      return;
    }

    this.summary.set(null);
    const value = this.form.getRawValue();
    this.submitted.emit({
      zone: value.zone.trim().toUpperCase(),
      aisle: value.aisle.trim(),
      shelf: value.shelf.trim().toUpperCase(),
      actingPerson: value.actingPerson.trim(),
    });
  }
}
