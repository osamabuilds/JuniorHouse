import { ChangeDetectionStrategy, Component, OnInit, computed, inject, input, output, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ApiError } from '@core/http';
import { LookupNameResolver } from '@features/reference-data';
import { ControlErrorsComponent, FieldErrorsComponent, inputNumber, missingSummary } from '@shared';
import { RecordMilestoneValue, RunDetailDto } from '../../models';
import { MILESTONE_FINISHED_READY_TO_SHIP } from '../../production.constants';
import { milestoneChoices, todayInPakistan } from '../../utils';

/**
 * Records a milestone the vendor reported (spec AC-4..AC-8, AC-17, AC-35, D11): what happened, on which day
 * (between the day the PO was sent and today), who at the vendor said so, and who at Romp is recording it.
 * "Finished and ready to ship" also takes the quantities per size and colour; leaving them all at 0 records the
 * milestone without quantities, since nothing else depends on it.
 */
@Component({
  selector: 'app-milestone-form',
  imports: [ReactiveFormsModule, ControlErrorsComponent, FieldErrorsComponent],
  template: `
    <form class="card" [formGroup]="form" (ngSubmit)="submit()" aria-labelledby="milestone-heading" novalidate>
      <h3 id="milestone-heading">Record a milestone</h3>
      <p class="field-hint">Record what the vendor has told you. Steps you skip are fine: only bulk cutting has to wait for an approved sample.</p>

      @if (error()) {
        <p class="banner banner--error" role="alert">{{ error()?.message }}</p>
      }
      @if (summary()) {
        <p class="banner banner--error" role="alert">{{ summary() }}</p>
      }

      <div class="form-grid">
        <div class="form-field">
          <label for="ms-type">What happened</label>
          <select id="ms-type" formControlName="typeId">
            <option [ngValue]="null">Choose…</option>
            @for (choice of choices(); track choice.id) {
              <option [ngValue]="choice.id" [disabled]="choice.disabledReason !== null">
                {{ choice.label }}{{ choice.disabledReason ? ' (' + choice.disabledReason + ')' : '' }}
              </option>
            }
          </select>
          <app-control-errors [control]="form.controls.typeId" label="Milestone" kind="choose" />
          <app-field-errors [messages]="fieldErrors('TypeId')" />
        </div>

        <div class="form-field">
          <label for="ms-date">Date it happened</label>
          <input id="ms-date" type="date" formControlName="claimedDate" [min]="minDate()" [max]="today" />
          <p class="field-hint">Between {{ minDate() }} (when the PO was sent) and today.</p>
          <app-control-errors [control]="form.controls.claimedDate" label="Date it happened" />
          <app-field-errors [messages]="fieldErrors('ClaimedDate')" />
        </div>

        <div class="form-field">
          <label for="ms-reporter">Who at the vendor told you</label>
          <input id="ms-reporter" type="text" formControlName="reportedBy" maxlength="100" />
          <app-control-errors [control]="form.controls.reportedBy" label="Vendor contact" />
          <app-field-errors [messages]="fieldErrors('ReportedBy')" />
        </div>

        <div class="form-field">
          <label for="ms-person">Your name</label>
          <input id="ms-person" type="text" formControlName="actingPerson" maxlength="100" autocomplete="name" />
          <app-control-errors [control]="form.controls.actingPerson" label="Your name" />
          <app-field-errors [messages]="fieldErrors('ActingPerson')" />
        </div>

        <div class="form-field form-field--wide">
          <label for="ms-note">Note (optional)</label>
          <textarea id="ms-note" rows="2" formControlName="note" maxlength="500"></textarea>
          <app-field-errors [messages]="fieldErrors('Note')" />
        </div>
      </div>

      @if (isFinished()) {
        <fieldset>
          <legend>How many are finished? (optional)</legend>
          <table class="responsive line-grid">
            <caption class="sr-only">
              Finished pieces
            </caption>
            <thead>
              <tr>
                <th scope="col">Size</th>
                <th scope="col">Colour</th>
                <th scope="col">Ordered</th>
                <th scope="col">Finished</th>
              </tr>
            </thead>
            <tbody>
              @for (row of run().quantities; track row.sizeId + '-' + row.colourId) {
                <tr>
                  <td data-label="Size">{{ names().sizeName(row.sizeId) }}</td>
                  <td data-label="Colour">{{ names().colourName(row.colourId) }}</td>
                  <td data-label="Ordered">{{ row.ordered }}</td>
                  <td data-label="Finished">
                    <label class="sr-only" [for]="'ms-fin-' + row.sizeId + '-' + row.colourId">
                      Finished {{ names().sizeName(row.sizeId) }} {{ names().colourName(row.colourId) }}
                    </label>
                    <input
                      [id]="'ms-fin-' + row.sizeId + '-' + row.colourId"
                      type="number"
                      min="0"
                      step="1"
                      [value]="finished()[row.sizeId + '-' + row.colourId] ?? 0"
                      (input)="onFinished(row.sizeId, row.colourId, $event)"
                    />
                  </td>
                </tr>
              }
            </tbody>
          </table>
          <app-field-errors [messages]="fieldErrors('FinishedLines')" />
        </fieldset>
      }

      <div class="form-actions">
        <button type="submit" class="button button--primary" [disabled]="saving()">{{ saving() ? 'Saving…' : 'Record milestone' }}</button>
        <button type="button" class="button" (click)="cancelled.emit()">Cancel</button>
      </div>
    </form>
  `,
  styles: ':host { display: block; margin-bottom: var(--space-5); }',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MilestoneFormComponent implements OnInit {
  private readonly formBuilder = inject(FormBuilder);

  readonly run = input.required<RunDetailDto>();
  readonly names = input.required<LookupNameResolver>();
  readonly saving = input(false);
  readonly error = input<ApiError | null>(null);
  /** The name last used in this browser, so the field starts filled. */
  readonly defaultPerson = input('');

  readonly submitted = output<RecordMilestoneValue>();
  readonly cancelled = output<void>();

  readonly today = todayInPakistan();
  readonly minDate = computed(() => this.run().poSentDate);
  readonly choices = computed(() => milestoneChoices(this.run()));
  readonly summary = signal<string | null>(null);
  readonly finished = signal<Readonly<Record<string, number>>>({});

  readonly form = this.formBuilder.group({
    typeId: this.formBuilder.control<number | null>(null, Validators.required),
    claimedDate: this.formBuilder.nonNullable.control(this.today, Validators.required),
    reportedBy: this.formBuilder.nonNullable.control('', [Validators.required, Validators.maxLength(100)]),
    actingPerson: this.formBuilder.nonNullable.control('', [Validators.required, Validators.minLength(2), Validators.maxLength(100)]),
    note: this.formBuilder.nonNullable.control('', Validators.maxLength(500)),
  });

  private readonly selectedType = signal<number | null>(null);
  readonly isFinished = computed(() => this.selectedType() === MILESTONE_FINISHED_READY_TO_SHIP);

  ngOnInit(): void {
    this.form.patchValue({ actingPerson: this.defaultPerson() });
    this.form.controls.typeId.valueChanges.subscribe((value) => this.selectedType.set(value));
  }

  fieldErrors(field: string): readonly string[] {
    return this.error()?.fieldErrors[field] ?? [];
  }

  onFinished(sizeId: number, colourId: number, event: Event): void {
    this.finished.set({ ...this.finished(), [`${sizeId}-${colourId}`]: inputNumber(event) });
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.summary.set(
        missingSummary(this.form.controls, {
          typeId: 'Milestone',
          claimedDate: 'Date it happened',
          reportedBy: 'Vendor contact',
          actingPerson: 'Your name',
        }),
      );
      return;
    }

    this.summary.set(null);
    const raw = this.form.getRawValue();
    const finishedLines = this.isFinished()
      ? this.run()
          .quantities.map((row) => ({ sizeId: row.sizeId, colourId: row.colourId, qty: this.finished()[`${row.sizeId}-${row.colourId}`] ?? 0 }))
          .filter((line) => line.qty > 0)
      : [];

    this.submitted.emit({
      typeId: raw.typeId!,
      claimedDate: raw.claimedDate,
      reportedBy: raw.reportedBy.trim(),
      actingPerson: raw.actingPerson.trim(),
      note: raw.note.trim() || null,
      finishedLines: finishedLines.length > 0 ? finishedLines : null,
    });
  }
}
