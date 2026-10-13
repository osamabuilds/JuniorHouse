import { ChangeDetectionStrategy, Component, OnInit, inject, input, output, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ApiError } from '@core/http';
import { LookupNameResolver } from '@features/reference/lookups';
import { ControlErrorsComponent, FieldErrorsComponent, inputChecked, inputValue, selectValue } from '@shared';
import { BatchLineDto, InspectionDto, RecordSafetyValue, SafetyCheckDto } from '../../models';
import { CHECK_FAIL, CHECK_NOT_APPLICABLE, CHECK_PASS, CHECK_RESULT_LABELS, SAFETY_TYPES } from '../../batches.constants';
import { lineKey } from '../../utils';

interface SafetyRow {
  readonly resultId: number;
  readonly reason: string;
  /** "size-colour" keys of the affected lines of a failed check; empty means the whole batch. */
  readonly lines: readonly string[];
}

/**
 * The two mandatory child-safety checks (BRD 5.11, non-negotiable; spec AC-31..AC-34): choking hazard on small trims and drawstring
 * safety on necklines. Each is Pass, Fail or Not applicable (a reason is required for Not applicable); a failed check may name the
 * size and colour lines it applies to, and quantity on those lines can then only be rejected or sent for rework. Nothing can be
 * accepted until both results are recorded, and there is no way to override that.
 */
@Component({
  selector: 'app-safety-checks-panel',
  imports: [ReactiveFormsModule, ControlErrorsComponent, FieldErrorsComponent],
  template: `
    <section class="card" aria-labelledby="safety-heading">
      <h3 id="safety-heading">Child-safety checks</h3>

      @if (recorded() && !editing()) {
        <ul class="safety-results">
          @for (check of inspection().safetyChecks; track check.safetyTypeId) {
            <li>
              <strong>{{ typeLabel(check.safetyTypeId) }}:</strong>
              <span
                class="qc-flag"
                [class.qc-flag--ok]="check.resultId !== fail"
                [class.qc-flag--danger]="check.resultId === fail"
                >{{ resultLabels[check.resultId] }}</span
              >
              @if (check.reason) {
                <span class="field-hint"> {{ check.reason }}</span>
              }
              @if (check.resultId === fail) {
                <span class="field-hint">
                  Applies to
                  @if (check.affectedLines.length === 0) {
                    the whole batch.
                  } @else {
                    {{ affectedText(check) }}.
                  }
                  That quantity can only be rejected or sent back for rework.
                </span>
              }
            </li>
          }
        </ul>
        @if (canChange()) {
          <button type="button" class="button button--small" (click)="startEditing()">Correct the safety checks</button>
        }
      } @else if (!canChange()) {
        <p class="state-message">These results are final: a decision has already been recorded for this quantity.</p>
      } @else {
        <form [formGroup]="form" (ngSubmit)="submit()" novalidate>
          <p class="field-hint">Both checks are required before anything can be accepted. Record what you found for each.</p>

          @if (error()) {
            <p class="banner banner--error" role="alert">{{ error()?.message }}</p>
          }
          @if (summary()) {
            <p class="banner banner--error" role="alert">{{ summary() }}</p>
          }

          @for (type of types; track type.id) {
            <fieldset class="qc-repeat">
              <legend>{{ type.label }}</legend>
              <p class="field-hint">{{ type.hint }}</p>
              <div class="form-field">
                <label [for]="'sf-result-' + type.id">Result</label>
                <select [id]="'sf-result-' + type.id" (change)="onResult(type.id, $event)">
                  <option [value]="pass" [selected]="rows()[type.id].resultId === pass">Pass</option>
                  <option [value]="fail" [selected]="rows()[type.id].resultId === fail">Fail</option>
                  <option [value]="notApplicable" [selected]="rows()[type.id].resultId === notApplicable">Not applicable</option>
                </select>
              </div>

              @if (rows()[type.id].resultId === notApplicable) {
                <div class="form-field">
                  <label [for]="'sf-reason-' + type.id">Why it does not apply (required)</label>
                  <textarea [id]="'sf-reason-' + type.id" rows="2" maxlength="500" [value]="rows()[type.id].reason" (input)="onReason(type.id, $event)"></textarea>
                </div>
              }

              @if (rows()[type.id].resultId === fail) {
                <fieldset>
                  <legend>Which sizes and colours failed?</legend>
                  <p class="field-hint">Tick the lines that failed. Tick none if the whole batch failed.</p>
                  @for (line of lines(); track lineId(line)) {
                    <label class="toolbar__checkbox">
                      <input type="checkbox" [checked]="rows()[type.id].lines.includes(lineId(line))" (change)="onLine(type.id, lineId(line), $event)" />
                      {{ names().sizeName(line.sizeId) }} / {{ names().colourName(line.colourId) }}
                    </label>
                  }
                </fieldset>
              }
            </fieldset>
          }

          <div class="form-field">
            <label for="sf-person">Your name</label>
            <input id="sf-person" type="text" formControlName="actingPerson" maxlength="100" autocomplete="name" />
            <app-control-errors [control]="form.controls.actingPerson" label="Your name" />
            <app-field-errors [messages]="fieldErrors('ActingPerson')" />
          </div>

          <div class="form-actions">
            <button type="submit" class="button button--primary" [disabled]="saving()">{{ saving() ? 'Saving…' : 'Record safety checks' }}</button>
            @if (recorded()) {
              <button type="button" class="button" (click)="editing.set(false)">Cancel</button>
            }
          </div>
        </form>
      }
    </section>
  `,
  styles: ':host { display: block; margin-bottom: var(--space-5); } .safety-results { list-style: none; margin: 0 0 var(--space-3); padding: 0; } .safety-results li { margin-bottom: var(--space-2); }',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SafetyChecksPanelComponent implements OnInit {
  private readonly formBuilder = inject(FormBuilder);

  readonly inspection = input.required<InspectionDto>();
  /** The size and colour lines of what was inspected. */
  readonly lines = input.required<readonly BatchLineDto[]>();
  readonly names = input.required<LookupNameResolver>();
  /** False once the covered quantity has been decided: the results are then frozen. */
  readonly canChange = input(true);
  readonly saving = input(false);
  readonly error = input<ApiError | null>(null);
  readonly defaultPerson = input('');

  readonly submitted = output<RecordSafetyValue>();

  readonly types = SAFETY_TYPES;
  readonly resultLabels = CHECK_RESULT_LABELS;
  readonly pass = CHECK_PASS;
  readonly fail = CHECK_FAIL;
  readonly notApplicable = CHECK_NOT_APPLICABLE;

  readonly editing = signal(false);
  readonly summary = signal<string | null>(null);
  readonly rows = signal<Readonly<Record<number, SafetyRow>>>(Object.fromEntries(SAFETY_TYPES.map((t) => [t.id, { resultId: CHECK_PASS, reason: '', lines: [] }])));

  readonly form = this.formBuilder.group({
    actingPerson: this.formBuilder.nonNullable.control('', [Validators.required, Validators.minLength(2), Validators.maxLength(100)]),
  });

  recorded(): boolean {
    return this.inspection().safetyChecks.length >= 2;
  }

  ngOnInit(): void {
    this.form.patchValue({ actingPerson: this.defaultPerson() });
  }

  typeLabel(id: number): string {
    return SAFETY_TYPES.find((t) => t.id === id)?.label ?? '';
  }

  lineId(line: BatchLineDto): string {
    return lineKey(line.sizeId, line.colourId);
  }

  affectedText(check: SafetyCheckDto): string {
    return check.affectedLines.map((l) => `${this.names().sizeName(l.sizeId)} / ${this.names().colourName(l.colourId)}`).join(', ');
  }

  fieldErrors(field: string): readonly string[] {
    return this.error()?.fieldErrors[field] ?? [];
  }

  /** Starts a correction from what is recorded now. */
  startEditing(): void {
    this.rows.set(
      Object.fromEntries(
        SAFETY_TYPES.map((t) => {
          const existing = this.inspection().safetyChecks.find((c) => c.safetyTypeId === t.id);
          return [
            t.id,
            {
              resultId: existing?.resultId ?? CHECK_PASS,
              reason: existing?.reason ?? '',
              lines: existing?.affectedLines.map((l) => lineKey(l.sizeId, l.colourId)) ?? [],
            },
          ];
        }),
      ),
    );
    this.editing.set(true);
  }

  onResult(typeId: number, event: Event): void {
    this.patch(typeId, { resultId: +selectValue(event) });
  }

  onReason(typeId: number, event: Event): void {
    this.patch(typeId, { reason: inputValue(event) });
  }

  onLine(typeId: number, key: string, event: Event): void {
    const current = this.rows()[typeId].lines;
    this.patch(typeId, { lines: inputChecked(event) ? [...current, key] : current.filter((k) => k !== key) });
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.summary.set("This can't be saved yet. Still needed: your name.");
      return;
    }

    for (const type of SAFETY_TYPES) {
      const row = this.rows()[type.id];
      if (row.resultId === CHECK_NOT_APPLICABLE && !row.reason.trim()) {
        this.summary.set(`Say why "${type.label}" does not apply.`);
        return;
      }
    }

    this.summary.set(null);
    this.editing.set(false);
    this.submitted.emit({
      inspectionId: this.inspection().id,
      actingPerson: this.form.getRawValue().actingPerson.trim(),
      checks: SAFETY_TYPES.map((type) => {
        const row = this.rows()[type.id];
        return {
          safetyTypeId: type.id,
          resultId: row.resultId,
          reason: row.resultId === CHECK_NOT_APPLICABLE ? row.reason.trim() : null,
          affectedLines:
            row.resultId === CHECK_FAIL
              ? row.lines.map((key) => {
                  const [sizeId, colourId] = key.split('-').map(Number);
                  return { sizeId, colourId };
                })
              : [],
        };
      }),
    });
  }

  private patch(typeId: number, change: Partial<SafetyRow>): void {
    this.rows.set({ ...this.rows(), [typeId]: { ...this.rows()[typeId], ...change } });
  }
}
