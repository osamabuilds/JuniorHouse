import { ChangeDetectionStrategy, Component, OnInit, computed, inject, input, output, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ApiError } from '@core/http';
import { LookupNameResolver } from '@features/reference-data';
import { ControlErrorsComponent, FieldErrorsComponent, inputNumber, inputValue, selectValue } from '@shared';
import { BatchLineDto, CountedLineDto, DecideBatchValue, DecisionPartValue } from '../../models';
import { OUTCOMES_NEEDING_REASON, OUTCOME_ACCEPTED, OUTCOME_LABELS } from '../../quality.constants';
import { lineKey, splitAddsUp, splitBalance } from '../../utils';

interface PartRow {
  readonly id: number;
  readonly outcomeId: number;
  readonly reason: string;
  /** Pieces per "size-colour" key. */
  readonly qty: Readonly<Record<string, number>>;
}

/**
 * Records the outcome of an inspected batch (BRD 5.11, spec AC-19..AC-25): every part is exactly one of Accept, Accept with
 * concession, Reject or Rework; a reason is required for everything except Accept; and when only part of a batch failed it can
 * be split so the passing part is released now and does not wait for the rest (AC-23). A split must place every piece: the
 * balance below each size and colour says what is still to place, and the button stays off until it adds up.
 */
@Component({
  selector: 'app-outcome-panel',
  imports: [ReactiveFormsModule, ControlErrorsComponent, FieldErrorsComponent],
  template: `
    <form class="card" [formGroup]="form" (ngSubmit)="submit()" aria-labelledby="outcome-heading" novalidate>
      <h3 id="outcome-heading">Record the outcome</h3>

      @if (!safetyRecorded()) {
        <p class="banner banner--error" role="note">
          Both child-safety checks must be recorded before anything can be accepted. Until then you can only reject or send back for rework.
        </p>
      }

      @if (error()) {
        <p class="banner banner--error" role="alert">{{ error()?.message }}</p>
      }
      @if (summary()) {
        <p class="banner banner--error" role="alert">{{ summary() }}</p>
      }

      <fieldset>
        <legend>How much of the batch does this cover?</legend>
        <label class="toolbar__checkbox">
          <input type="radio" name="outcome-mode" value="whole" [checked]="mode() === 'whole'" (change)="setMode('whole')" />
          The whole batch gets one outcome
        </label>
        <label class="toolbar__checkbox">
          <input type="radio" name="outcome-mode" value="split" [checked]="mode() === 'split'" (change)="setMode('split')" />
          Split it: only part of it failed
        </label>
      </fieldset>

      @for (part of parts(); track part.id; let i = $index) {
        <fieldset class="qc-repeat">
          <legend>{{ mode() === 'whole' ? 'Outcome' : 'Part ' + (i + 1) }}</legend>
          <div class="form-grid">
            <div class="form-field">
              <label [for]="'oc-out-' + part.id">Outcome</label>
              <select [id]="'oc-out-' + part.id" (change)="onOutcome(part.id, $event)">
                @for (outcome of outcomes; track outcome.id) {
                  <option [value]="outcome.id" [selected]="part.outcomeId === outcome.id">{{ outcome.label }}</option>
                }
              </select>
            </div>
            <div class="form-field form-field--wide">
              <label [for]="'oc-reason-' + part.id">Reason{{ needsReason(part) ? ' (required)' : ' (optional)' }}</label>
              <textarea [id]="'oc-reason-' + part.id" rows="2" maxlength="500" [value]="part.reason" (input)="onReason(part.id, $event)"></textarea>
            </div>
          </div>

          @if (mode() === 'split') {
            <table class="responsive line-grid">
              <caption class="sr-only">
                Pieces in part {{ i + 1 }}
              </caption>
              <thead>
                <tr>
                  <th scope="col">Size</th>
                  <th scope="col">Colour</th>
                  <th scope="col">Pieces in this part</th>
                </tr>
              </thead>
              <tbody>
                @for (line of lines(); track lineId(line)) {
                  <tr>
                    <td data-label="Size">{{ names().sizeName(line.sizeId) }}</td>
                    <td data-label="Colour">{{ names().colourName(line.colourId) }}</td>
                    <td data-label="Pieces in this part">
                      <label class="sr-only" [for]="'oc-q-' + part.id + '-' + lineId(line)">
                        Part {{ i + 1 }}: {{ names().sizeName(line.sizeId) }} {{ names().colourName(line.colourId) }}
                      </label>
                      <input
                        [id]="'oc-q-' + part.id + '-' + lineId(line)"
                        type="number"
                        min="0"
                        step="1"
                        [value]="part.qty[lineId(line)] ?? 0"
                        (input)="onQty(part.id, lineId(line), $event)"
                      />
                    </td>
                  </tr>
                }
              </tbody>
            </table>
            @if (parts().length > 2) {
              <button type="button" class="button button--small" (click)="removePart(part.id)">Remove part {{ i + 1 }}</button>
            }
          }
        </fieldset>
      }

      @if (mode() === 'split') {
        <button type="button" class="button" (click)="addPart()">Add another part</button>

        <table class="responsive" aria-live="polite">
          <caption>
            Does every piece have a part?
          </caption>
          <thead>
            <tr>
              <th scope="col">Size and colour</th>
              <th scope="col">In the batch</th>
              <th scope="col">Placed</th>
              <th scope="col">Balance</th>
            </tr>
          </thead>
          <tbody>
            @for (row of balance(); track row.sizeId + '-' + row.colourId) {
              <tr>
                <td data-label="Size and colour">{{ names().sizeName(row.sizeId) }} / {{ names().colourName(row.colourId) }}</td>
                <td data-label="In the batch">{{ row.batchQty }}</td>
                <td data-label="Placed">{{ row.assigned }}</td>
                <td data-label="Balance">
                  @if (row.remaining === 0) {
                    <span class="qc-flag qc-flag--ok">All placed</span>
                  } @else if (row.remaining > 0) {
                    <span class="qc-flag qc-flag--warn">{{ row.remaining }} still to place</span>
                  } @else {
                    <span class="qc-flag qc-flag--danger">{{ -row.remaining }} too many</span>
                  }
                </td>
              </tr>
            }
          </tbody>
        </table>
      }

      <div class="form-field">
        <label for="oc-person">Your name</label>
        <input id="oc-person" type="text" formControlName="actingPerson" maxlength="100" autocomplete="name" />
        <app-control-errors [control]="form.controls.actingPerson" label="Your name" />
        <app-field-errors [messages]="fieldErrors('ActingPerson')" />
      </div>

      <div class="form-actions">
        <button type="submit" class="button button--primary" [disabled]="saving() || !canSubmit()">
          {{ saving() ? 'Saving…' : 'Record outcome' }}
        </button>
        <button type="button" class="button" (click)="cancelled.emit()">Cancel</button>
      </div>
    </form>
  `,
  styles: ':host { display: block; margin-bottom: var(--space-5); }',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class OutcomePanelComponent implements OnInit {
  private readonly formBuilder = inject(FormBuilder);
  private nextPartId = 1;

  readonly lines = input.required<readonly BatchLineDto[]>();
  readonly names = input.required<LookupNameResolver>();
  /** Whether both child-safety checks are recorded; when not, the panel says accepting is not possible yet. */
  readonly safetyRecorded = input(true);
  readonly saving = input(false);
  readonly error = input<ApiError | null>(null);
  readonly defaultPerson = input('');

  readonly submitted = output<DecideBatchValue>();
  readonly cancelled = output<void>();

  readonly outcomes = Object.entries(OUTCOME_LABELS).map(([id, label]) => ({ id: +id, label }));
  readonly mode = signal<'whole' | 'split'>('whole');
  readonly parts = signal<readonly PartRow[]>([]);
  readonly summary = signal<string | null>(null);

  readonly form = this.formBuilder.group({
    actingPerson: this.formBuilder.nonNullable.control('', [Validators.required, Validators.minLength(2), Validators.maxLength(100)]),
  });

  readonly balance = computed(() =>
    splitBalance(
      this.lines(),
      this.parts().map((part) => this.partLines(part)),
    ),
  );

  /** The button is off while a split does not add up or a part is missing its reason. */
  readonly canSubmit = computed(() => {
    if (this.parts().some((part) => this.needsReason(part) && !part.reason.trim())) {
      return false;
    }
    if (this.mode() === 'whole') {
      return true;
    }
    return splitAddsUp(this.balance()) && this.parts().every((part) => this.partLines(part).length > 0);
  });

  ngOnInit(): void {
    this.form.patchValue({ actingPerson: this.defaultPerson() });
    this.setMode('whole');
  }

  lineId(line: BatchLineDto): string {
    return lineKey(line.sizeId, line.colourId);
  }

  needsReason(part: PartRow): boolean {
    return OUTCOMES_NEEDING_REASON.includes(part.outcomeId);
  }

  fieldErrors(field: string): readonly string[] {
    return this.error()?.fieldErrors[field] ?? [];
  }

  setMode(mode: 'whole' | 'split'): void {
    this.mode.set(mode);
    const everything = Object.fromEntries(this.lines().map((l) => [lineKey(l.sizeId, l.colourId), l.qty]));
    this.parts.set(
      mode === 'whole'
        ? [{ id: this.nextPartId++, outcomeId: OUTCOME_ACCEPTED, reason: '', qty: everything }]
        : [
            { id: this.nextPartId++, outcomeId: OUTCOME_ACCEPTED, reason: '', qty: everything },
            { id: this.nextPartId++, outcomeId: 3, reason: '', qty: {} },
          ],
    );
  }

  addPart(): void {
    this.parts.set([...this.parts(), { id: this.nextPartId++, outcomeId: 3, reason: '', qty: {} }]);
  }

  removePart(id: number): void {
    this.parts.set(this.parts().filter((p) => p.id !== id));
  }

  onOutcome(id: number, event: Event): void {
    this.patch(id, { outcomeId: +selectValue(event) });
  }

  onReason(id: number, event: Event): void {
    this.patch(id, { reason: inputValue(event) });
  }

  onQty(id: number, key: string, event: Event): void {
    const part = this.parts().find((p) => p.id === id);
    if (part) {
      this.patch(id, { qty: { ...part.qty, [key]: Math.max(0, Math.trunc(inputNumber(event)) || 0) } });
    }
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.summary.set("This can't be saved yet. Still needed: your name.");
      return;
    }

    if (!this.canSubmit()) {
      this.summary.set(
        this.mode() === 'split' && !splitAddsUp(this.balance())
          ? 'Every piece needs a part: see the balance table.'
          : 'Give a reason for every outcome except Accept.',
      );
      return;
    }

    this.summary.set(null);
    const parts: DecisionPartValue[] = this.parts().map((part) => ({
      outcomeId: part.outcomeId,
      reason: part.reason.trim() || null,
      lines: this.mode() === 'whole' ? null : this.partLines(part),
    }));
    this.submitted.emit({ actingPerson: this.form.getRawValue().actingPerson.trim(), parts });
  }

  private partLines(part: PartRow): CountedLineDto[] {
    return this.lines()
      .map((l) => ({ sizeId: l.sizeId, colourId: l.colourId, qty: part.qty[lineKey(l.sizeId, l.colourId)] ?? 0 }))
      .filter((l) => l.qty > 0);
  }

  private patch(id: number, change: Partial<PartRow>): void {
    this.parts.set(this.parts().map((p) => (p.id === id ? { ...p, ...change } : p)));
  }
}
