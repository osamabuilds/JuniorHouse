import { ChangeDetectionStrategy, Component, OnInit, computed, inject, input, output } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ApiError } from '@core/http';
import { AppDatePipe, ControlErrorsComponent, FieldErrorsComponent } from '@shared';
import { ExpectedCompletionChangeDto, ExpectedCompletionValue } from '../../models';

/**
 * The expected completion date (spec AC-15, AC-16): what the vendor currently says, every earlier estimate with who
 * changed it and why, and whether the date is later than the latest acceptable delivery minus the buffer. It can be
 * set once bulk cutting has started.
 */
@Component({
  selector: 'app-expected-completion-panel',
  imports: [ReactiveFormsModule, AppDatePipe, ControlErrorsComponent, FieldErrorsComponent],
  template: `
    <dl class="facts">
      <div>
        <dt>Expected completion</dt>
        <dd>
          {{ expectedDate() ? (expectedDate() | appDate) : cuttingStarted() ? 'Not set yet' : 'Set once bulk cutting has started' }}
        </dd>
      </div>
      <div>
        <dt>Latest acceptable delivery</dt>
        <dd>{{ latestAcceptableDate() | appDate }}</dd>
      </div>
    </dl>

    @if (atRisk()) {
      <p class="banner banner--error" role="note">
        <strong>At risk of being late.</strong> The expected completion date is later than the latest acceptable delivery date, less the
        safety buffer. Talk to the vendor now.
      </p>
    }

    @if (history().length > 0) {
      <h3>Earlier estimates</h3>
      <ol class="timeline">
        @for (change of history(); track change.changedAt) {
          <li>
            <span class="timeline__status">
              {{ change.oldDate ? (change.oldDate | appDate) + ' → ' : 'First estimate: ' }}{{ change.newDate | appDate }}
            </span>
            <span class="timeline__meta">{{ change.changedAt | appDate: 'datetime' }} &middot; {{ change.actingPerson }}</span>
            @if (change.reason) {
              <span class="timeline__reason">{{ change.reason }}</span>
            }
          </li>
        }
      </ol>
    }

    @if (formOpen() && cuttingStarted() && runOpen()) {
      <form class="card" [formGroup]="form" (ngSubmit)="submit()" aria-labelledby="expected-heading" novalidate>
        <h3 id="expected-heading">{{ expectedDate() ? 'Move the expected completion date' : 'Set the expected completion date' }}</h3>
        @if (error()) {
          <p class="banner banner--error" role="alert">{{ error()?.message }}</p>
        }
        <div class="form-grid">
          <div class="form-field">
            <label for="expected-date">New date</label>
            <input id="expected-date" type="date" formControlName="date" />
            <app-control-errors [control]="form.controls.date" label="New date" />
            <app-field-errors [messages]="fieldErrors('Date')" />
          </div>
          <div class="form-field">
            <label for="expected-person">Your name</label>
            <input id="expected-person" type="text" formControlName="actingPerson" maxlength="100" autocomplete="name" />
            <app-control-errors [control]="form.controls.actingPerson" label="Your name" />
            <app-field-errors [messages]="fieldErrors('ActingPerson')" />
          </div>
          <div class="form-field form-field--wide">
            <label for="expected-reason">Why it moved (optional)</label>
            <textarea id="expected-reason" rows="2" formControlName="reason" maxlength="500"></textarea>
            <app-field-errors [messages]="fieldErrors('Reason')" />
          </div>
        </div>
        <div class="form-actions">
          <button type="submit" class="button button--primary" [disabled]="saving()">{{ saving() ? 'Saving…' : 'Save date' }}</button>
          <button type="button" class="button" (click)="cancelled.emit()">Cancel</button>
        </div>
      </form>
    } @else if (cuttingStarted() && runOpen()) {
      <div class="panel__actions">
        <button type="button" class="button" (click)="opened.emit()">{{ expectedDate() ? 'Move the date' : 'Set the date' }}</button>
      </div>
    }
  `,
  styles: ':host { display: block; }',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ExpectedCompletionPanelComponent implements OnInit {
  private readonly formBuilder = inject(FormBuilder);

  readonly expectedDate = input<string | null>(null);
  readonly latestAcceptableDate = input<string | null>(null);
  readonly history = input<readonly ExpectedCompletionChangeDto[]>([]);
  readonly atRisk = input(false);
  readonly cuttingStarted = input(false);
  readonly runOpen = input(true);
  readonly formOpen = input(false);
  readonly saving = input(false);
  readonly error = input<ApiError | null>(null);
  readonly defaultPerson = input('');

  readonly opened = output<void>();
  readonly cancelled = output<void>();
  readonly submitted = output<ExpectedCompletionValue>();

  readonly hasHistory = computed(() => this.history().length > 0);

  readonly form = this.formBuilder.group({
    date: this.formBuilder.nonNullable.control('', Validators.required),
    actingPerson: this.formBuilder.nonNullable.control('', [Validators.required, Validators.minLength(2), Validators.maxLength(100)]),
    reason: this.formBuilder.nonNullable.control('', Validators.maxLength(500)),
  });

  ngOnInit(): void {
    this.form.patchValue({ actingPerson: this.defaultPerson(), date: this.expectedDate() ?? '' });
  }

  fieldErrors(field: string): readonly string[] {
    return this.error()?.fieldErrors[field] ?? [];
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const raw = this.form.getRawValue();
    this.submitted.emit({ date: raw.date, reason: raw.reason.trim() || null, actingPerson: raw.actingPerson.trim() });
  }
}
