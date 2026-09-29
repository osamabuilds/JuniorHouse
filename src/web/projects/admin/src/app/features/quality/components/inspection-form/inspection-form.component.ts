import { ChangeDetectionStrategy, Component, OnInit, computed, inject, input, output, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ApiError } from '@core/http';
import { LookupNameResolver } from '@features/reference-data';
import { ControlErrorsComponent, FieldErrorsComponent, inputNumber, inputValue, missingSummary, selectValue } from '@shared';
import { AqlPlanDto, BatchLineDto, DefectDto, RecordInspectionValue } from '../../models';
import {
  CHECK_FAIL,
  CHECK_PASS,
  CHECK_TYPES,
  DEFECT_TYPES,
  DEFECT_TYPE_FOR_CHECK,
  MODE_LABELS,
  SEVERITIES,
} from '../../quality.constants';
import { lineKey, todayInPakistan } from '../../utils';

interface DefectRow {
  readonly id: number;
  readonly defectTypeId: number;
  readonly severityId: number;
  readonly qty: number;
  /** "size-colour" or "" for "not tied to one size and colour". */
  readonly line: string;
  readonly note: string;
}

/**
 * Records an inspection round (BRD 5.11, spec AC-13..AC-18, AC-30): the AQL plan that will be applied, one Pass/Fail per tech-pack
 * item, and every defect found with its type, severity, quantity and a written note. A failed item needs a defect of its type.
 * The result is computed by the API from the defects and the plan, never typed in.
 */
@Component({
  selector: 'app-inspection-form',
  imports: [ReactiveFormsModule, ControlErrorsComponent, FieldErrorsComponent],
  template: `
    <form class="card" [formGroup]="form" (ngSubmit)="submit()" [attr.aria-labelledby]="'insp-heading'" novalidate>
      <h3 id="insp-heading">{{ heading() }}</h3>

      @if (plan(); as plan) {
        <p class="banner banner--info" role="note" id="insp-plan">
          <strong>{{ modeLabels[plan.modeCode] }}.</strong>
          @if (plan.modeCode === 'Full') {
            This is a small lot, so inspect all {{ plan.sampleQty }} pieces.
          } @else {
            Inspect {{ plan.sampleQty }} of the {{ plan.lotQty }} pieces (code letter {{ plan.codeLetter }}).
          }
          Up to {{ plan.acceptNo }} major or minor defects passes; {{ plan.rejectNo }} or more fails. Any critical defect fails.
        </p>
      }

      @if (error()) {
        <p class="banner banner--error" role="alert">{{ error()?.message }}</p>
      }
      @if (summary()) {
        <p class="banner banner--error" role="alert">{{ summary() }}</p>
      }

      <fieldset>
        <legend>Checklist against the tech pack</legend>
        <table class="responsive">
          <caption class="sr-only">
            Tech-pack checklist
          </caption>
          <thead>
            <tr>
              <th scope="col">Check</th>
              <th scope="col">Result</th>
            </tr>
          </thead>
          <tbody>
            @for (check of checkTypes; track check.id) {
              <tr>
                <td data-label="Check">
                  <label [for]="'chk-' + check.id">{{ check.label }}</label>
                </td>
                <td data-label="Result">
                  <select [id]="'chk-' + check.id" (change)="onCheck(check.id, $event)">
                    <option [value]="pass" [selected]="results()[check.id] === pass">Pass</option>
                    <option [value]="fail" [selected]="results()[check.id] === fail">Fail</option>
                  </select>
                </td>
              </tr>
            }
          </tbody>
        </table>
      </fieldset>

      <fieldset>
        <legend>Defects found</legend>
        @if (defects().length === 0) {
          <p class="field-hint">No defects recorded. Add one for every problem you find.</p>
        }
        @for (defect of defects(); track defect.id; let i = $index) {
          <div class="qc-repeat" role="group" [attr.aria-label]="'Defect ' + (i + 1)">
            <div class="form-grid">
              <div class="form-field">
                <label [for]="'df-type-' + defect.id">Type</label>
                <select [id]="'df-type-' + defect.id" (change)="onDefect(defect.id, 'defectTypeId', $event)">
                  @for (type of defectTypes; track type.id) {
                    <option [value]="type.id" [selected]="defect.defectTypeId === type.id">{{ type.label }}</option>
                  }
                </select>
              </div>
              <div class="form-field">
                <label [for]="'df-sev-' + defect.id">Severity</label>
                <select [id]="'df-sev-' + defect.id" (change)="onDefect(defect.id, 'severityId', $event)">
                  @for (severity of severities; track severity.id) {
                    <option [value]="severity.id" [selected]="defect.severityId === severity.id">{{ severity.label }}</option>
                  }
                </select>
              </div>
              <div class="form-field">
                <label [for]="'df-qty-' + defect.id">Pieces affected</label>
                <input [id]="'df-qty-' + defect.id" type="number" min="1" step="1" [value]="defect.qty" (input)="onDefect(defect.id, 'qty', $event)" />
              </div>
              <div class="form-field">
                <label [for]="'df-line-' + defect.id">Size and colour (optional)</label>
                <select [id]="'df-line-' + defect.id" (change)="onDefect(defect.id, 'line', $event)">
                  <option value="" [selected]="defect.line === ''">Not tied to one size and colour</option>
                  @for (line of lines(); track lineId(line)) {
                    <option [value]="lineId(line)" [selected]="defect.line === lineId(line)">{{ names().sizeName(line.sizeId) }} / {{ names().colourName(line.colourId) }}</option>
                  }
                </select>
              </div>
              <div class="form-field form-field--wide">
                <label [for]="'df-note-' + defect.id">What is wrong (required)</label>
                <textarea [id]="'df-note-' + defect.id" rows="2" maxlength="500" [value]="defect.note" (input)="onDefect(defect.id, 'note', $event)"></textarea>
              </div>
            </div>
            <button type="button" class="button button--small" (click)="removeDefect(defect.id)">Remove defect {{ i + 1 }}</button>
          </div>
        }
        <button type="button" class="button" (click)="addDefect()">Add a defect</button>
        <app-field-errors [messages]="fieldErrors('Defects')" />
      </fieldset>

      <div class="form-grid">
        <div class="form-field">
          <label for="insp-date">Date inspected</label>
          <input id="insp-date" type="date" formControlName="inspectionDate" [max]="today" />
          <app-control-errors [control]="form.controls.inspectionDate" label="Date inspected" />
          <app-field-errors [messages]="fieldErrors('InspectionDate')" />
        </div>

        <div class="form-field">
          <label for="insp-sample">Pieces inspected (optional)</label>
          <input id="insp-sample" type="number" min="1" step="1" formControlName="sampleQty" [placeholder]="'' + (plan()?.sampleQty ?? '')" />
          <p class="field-hint">Leave empty to use the plan's number. Never fewer than the plan asks for.</p>
          <app-field-errors [messages]="fieldErrors('SampleQty')" />
        </div>

        <div class="form-field">
          <label for="insp-person">Your name</label>
          <input id="insp-person" type="text" formControlName="actingPerson" maxlength="100" autocomplete="name" />
          <app-control-errors [control]="form.controls.actingPerson" label="Your name" />
          <app-field-errors [messages]="fieldErrors('ActingPerson')" />
        </div>
      </div>

      <div class="form-actions">
        <button type="submit" class="button button--primary" [disabled]="saving()">{{ saving() ? 'Saving…' : 'Record inspection' }}</button>
        <button type="button" class="button" (click)="cancelled.emit()">Cancel</button>
      </div>
    </form>
  `,
  styles: ':host { display: block; margin-bottom: var(--space-5); }',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class InspectionFormComponent implements OnInit {
  private readonly formBuilder = inject(FormBuilder);
  private nextDefectId = 1;

  /** The size and colour lines of what is being inspected (the whole batch, or one returned part). */
  readonly lines = input.required<readonly BatchLineDto[]>();
  readonly names = input.required<LookupNameResolver>();
  readonly plan = input<AqlPlanDto | null>(null);
  /** Null inspects the whole batch; an id inspects that returned part. */
  readonly subBatchId = input<number | null>(null);
  readonly saving = input(false);
  readonly error = input<ApiError | null>(null);
  /** The name last used in this browser, so the field starts filled. */
  readonly defaultPerson = input('');

  readonly submitted = output<RecordInspectionValue>();
  readonly cancelled = output<void>();

  readonly today = todayInPakistan();
  readonly checkTypes = CHECK_TYPES;
  readonly defectTypes = DEFECT_TYPES;
  readonly severities = SEVERITIES;
  readonly modeLabels = MODE_LABELS;
  readonly pass = CHECK_PASS;
  readonly fail = CHECK_FAIL;

  readonly summary = signal<string | null>(null);
  readonly results = signal<Readonly<Record<number, number>>>(Object.fromEntries(CHECK_TYPES.map((c) => [c.id, CHECK_PASS])));
  readonly defects = signal<readonly DefectRow[]>([]);

  readonly heading = computed(() => (this.subBatchId() === null ? 'Inspect the batch' : 'Re-inspect the returned part'));

  readonly form = this.formBuilder.group({
    inspectionDate: this.formBuilder.nonNullable.control(this.today, Validators.required),
    actingPerson: this.formBuilder.nonNullable.control('', [Validators.required, Validators.minLength(2), Validators.maxLength(100)]),
    sampleQty: this.formBuilder.control<number | null>(null),
  });

  ngOnInit(): void {
    this.form.patchValue({ actingPerson: this.defaultPerson() });
  }

  lineId(line: BatchLineDto): string {
    return lineKey(line.sizeId, line.colourId);
  }

  fieldErrors(field: string): readonly string[] {
    return this.error()?.fieldErrors[field] ?? [];
  }

  onCheck(checkTypeId: number, event: Event): void {
    this.results.set({ ...this.results(), [checkTypeId]: +selectValue(event) });
  }

  addDefect(): void {
    this.defects.set([...this.defects(), { id: this.nextDefectId++, defectTypeId: 5, severityId: 3, qty: 1, line: '', note: '' }]);
  }

  removeDefect(id: number): void {
    this.defects.set(this.defects().filter((d) => d.id !== id));
  }

  onDefect(id: number, field: 'defectTypeId' | 'severityId' | 'qty' | 'line' | 'note', event: Event): void {
    const raw = field === 'note' ? inputValue(event) : field === 'line' ? selectValue(event) : field === 'qty' ? inputNumber(event) : +selectValue(event);
    this.defects.set(this.defects().map((d) => (d.id === id ? { ...d, [field]: raw } : d)));
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.summary.set(missingSummary(this.form.controls, { inspectionDate: 'Date inspected', actingPerson: 'Your name' }));
      return;
    }

    const problem = this.defectProblem();
    if (problem) {
      this.summary.set(problem);
      return;
    }

    this.summary.set(null);
    const value = this.form.getRawValue();
    this.submitted.emit({
      subBatchId: this.subBatchId(),
      inspectionDate: value.inspectionDate,
      actingPerson: value.actingPerson.trim(),
      sampleQty: value.sampleQty ? Math.trunc(value.sampleQty) : null,
      checks: CHECK_TYPES.map((c) => ({ checkTypeId: c.id, resultId: this.results()[c.id] })),
      defects: this.defects().map((d): DefectDto => {
        const [sizeId, colourId] = d.line ? d.line.split('-').map(Number) : [null, null];
        return { defectTypeId: d.defectTypeId, severityId: d.severityId, qty: Math.trunc(d.qty), sizeId, colourId, note: d.note.trim() };
      }),
    });
  }

  /** What is wrong with the defect list, in words, or null when it is fine (the API checks the same rules). */
  private defectProblem(): string | null {
    const rows = this.defects();
    if (rows.some((d) => !(d.qty >= 1))) {
      return 'Each defect needs a number of pieces affected, 1 or more.';
    }
    if (rows.some((d) => !d.note.trim())) {
      return 'Each defect needs a written note saying what is wrong.';
    }
    for (const check of CHECK_TYPES) {
      if (this.results()[check.id] === CHECK_FAIL && !rows.some((d) => d.defectTypeId === DEFECT_TYPE_FOR_CHECK[check.id])) {
        return `"${check.label}" failed, so add at least one defect of that type.`;
      }
    }
    return null;
  }
}
