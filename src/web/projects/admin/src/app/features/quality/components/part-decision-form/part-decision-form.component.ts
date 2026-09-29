import { ChangeDetectionStrategy, Component, OnInit, computed, inject, input, output, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ApiError } from '@core/http';
import { ControlErrorsComponent, FieldErrorsComponent } from '@shared';
import { DecideSubBatchValue, SubBatchDto } from '../../models';
import { OUTCOMES_NEEDING_REASON, OUTCOME_ACCEPTED, OUTCOME_LABELS } from '../../quality.constants';

/**
 * Records the outcome of a returned rework part after it has been re-inspected (spec AC-26, AC-34): one outcome for the whole
 * part, under the same rules as the first decision (a reason for everything except Accept, no acceptance without both safety
 * checks on the re-inspection). Rework again is allowed: rounds are unlimited.
 */
@Component({
  selector: 'app-part-decision-form',
  imports: [ReactiveFormsModule, ControlErrorsComponent, FieldErrorsComponent],
  template: `
    <form class="card" [formGroup]="form" (ngSubmit)="submit()" aria-labelledby="pd-heading" novalidate>
      <h3 id="pd-heading">Outcome for returned part {{ part().splitNo }}</h3>
      <p class="field-hint">This is the part that came back from rework (round {{ part().reworkRoundCount + 1 }}).</p>

      @if (error()) {
        <p class="banner banner--error" role="alert">{{ error()?.message }}</p>
      }
      @if (summary()) {
        <p class="banner banner--error" role="alert">{{ summary() }}</p>
      }

      <div class="form-grid">
        <div class="form-field">
          <label for="pd-outcome">Outcome</label>
          <select id="pd-outcome" formControlName="outcomeId">
            @for (outcome of outcomes; track outcome.id) {
              <option [ngValue]="outcome.id">{{ outcome.label }}</option>
            }
          </select>
        </div>
        <div class="form-field">
          <label for="pd-person">Your name</label>
          <input id="pd-person" type="text" formControlName="actingPerson" maxlength="100" autocomplete="name" />
          <app-control-errors [control]="form.controls.actingPerson" label="Your name" />
          <app-field-errors [messages]="fieldErrors('ActingPerson')" />
        </div>
        <div class="form-field form-field--wide">
          <label for="pd-reason">Reason{{ needsReason() ? ' (required)' : ' (optional)' }}</label>
          <textarea id="pd-reason" rows="2" formControlName="reason" maxlength="500"></textarea>
          <app-field-errors [messages]="fieldErrors('Reason')" />
        </div>
      </div>

      <div class="form-actions">
        <button type="submit" class="button button--primary" [disabled]="saving()">{{ saving() ? 'Saving…' : 'Record outcome' }}</button>
        <button type="button" class="button" (click)="cancelled.emit()">Cancel</button>
      </div>
    </form>
  `,
  styles: ':host { display: block; margin-bottom: var(--space-5); }',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PartDecisionFormComponent implements OnInit {
  private readonly formBuilder = inject(FormBuilder);

  readonly part = input.required<SubBatchDto>();
  readonly saving = input(false);
  readonly error = input<ApiError | null>(null);
  readonly defaultPerson = input('');

  readonly submitted = output<DecideSubBatchValue>();
  readonly cancelled = output<void>();

  readonly outcomes = Object.entries(OUTCOME_LABELS).map(([id, label]) => ({ id: +id, label }));
  readonly summary = signal<string | null>(null);

  readonly form = this.formBuilder.group({
    outcomeId: this.formBuilder.nonNullable.control(OUTCOME_ACCEPTED),
    reason: this.formBuilder.nonNullable.control('', Validators.maxLength(500)),
    actingPerson: this.formBuilder.nonNullable.control('', [Validators.required, Validators.minLength(2), Validators.maxLength(100)]),
  });

  private readonly outcomeId = signal(OUTCOME_ACCEPTED);
  readonly needsReason = computed(() => OUTCOMES_NEEDING_REASON.includes(this.outcomeId()));

  ngOnInit(): void {
    this.form.patchValue({ actingPerson: this.defaultPerson() });
    this.form.controls.outcomeId.valueChanges.subscribe((id) => this.outcomeId.set(id));
  }

  fieldErrors(field: string): readonly string[] {
    return this.error()?.fieldErrors[field] ?? [];
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.summary.set("This can't be saved yet. Still needed: your name.");
      return;
    }

    const value = this.form.getRawValue();
    if (this.needsReason() && !value.reason.trim()) {
      this.summary.set('Give a reason for every outcome except Accept.');
      return;
    }

    this.summary.set(null);
    this.submitted.emit({
      subBatchId: this.part().id,
      outcomeId: value.outcomeId,
      reason: value.reason.trim() || null,
      actingPerson: value.actingPerson.trim(),
    });
  }
}
