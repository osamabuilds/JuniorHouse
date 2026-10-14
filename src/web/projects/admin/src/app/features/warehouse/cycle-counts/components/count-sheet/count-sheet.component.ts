import { ChangeDetectionStrategy, Component, OnInit, inject, input, output, signal } from '@angular/core';
import { FormArray, FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ControlErrorsComponent } from '@shared';
import { GRADES } from '../../cycle-counts.constants';
import { CycleCountDto, SubmitLinesValue } from '../../models';

/**
 * The blind count sheet (CC-2, CC-6, CC-10): pick a bin, then enter what is physically there, barcode and quantity per line. Nothing
 * on this screen can show what the system expects: the types it works with carry no system quantity. Entering no lines means "counted
 * the bin and found nothing". "Recount" is for a bin whose stock moved after the count opened (CC-7). Presentational only.
 */
@Component({
  selector: 'app-count-sheet',
  imports: [ReactiveFormsModule, ControlErrorsComponent],
  template: `
    <section class="card" aria-labelledby="cs-heading">
      <h2 id="cs-heading">Count the bins</h2>
      <p class="field-hint">Count what is on the shelf, not what you think should be there. What the books say stays hidden until you review.</p>

      <h3>Bins</h3>
      <ul>
        @for (bin of count().bins; track bin.binId) {
          <li>{{ bin.binCode }}: {{ bin.submitted ? 'counted' : 'not counted yet' }}</li>
        }
      </ul>

      @if (count().statusCode === 'OPEN') {
        <form [formGroup]="form" (ngSubmit)="submit()" novalidate>
          @if (summary()) {
            <p class="banner banner--error" role="alert">{{ summary() }}</p>
          }

          <div class="form-grid">
            <div class="form-field">
              <label for="cs-bin">Bin</label>
              <select id="cs-bin" formControlName="binCode">
                <option value="">Choose a bin…</option>
                @for (bin of count().bins; track bin.binId) {
                  <option [value]="bin.binCode">{{ bin.binCode }}</option>
                }
              </select>
              <app-control-errors [control]="form.controls.binCode" label="Bin" />
            </div>
            <div class="form-field">
              <label for="cs-person">Your name</label>
              <input id="cs-person" type="text" formControlName="actingPerson" maxlength="100" autocomplete="name" />
              <app-control-errors [control]="form.controls.actingPerson" label="Your name" />
            </div>
            <div class="form-field">
              <label class="checkbox">
                <input type="checkbox" formControlName="recount" />
                This is a recount (stock moved in this bin after the count opened)
              </label>
            </div>
          </div>

          <h3>What is in the bin</h3>
          <div formArrayName="lines">
            @for (line of lines.controls; track $index; let i = $index) {
              <div class="form-grid" [formGroupName]="i">
                <div class="form-field">
                  <label [for]="'cs-gtin-' + i">Barcode (13 digits)</label>
                  <input [id]="'cs-gtin-' + i" type="text" inputmode="numeric" formControlName="gtin" maxlength="13" autocomplete="off" />
                  <app-control-errors [control]="line.controls.gtin" label="Barcode" />
                </div>
                <div class="form-field">
                  <label [for]="'cs-grade-' + i">Grade</label>
                  <select [id]="'cs-grade-' + i" formControlName="gradeId">
                    @for (grade of grades; track grade.id) {
                      <option [ngValue]="grade.id">{{ grade.label }}</option>
                    }
                  </select>
                </div>
                <div class="form-field">
                  <label [for]="'cs-qty-' + i">How many you counted</label>
                  <input [id]="'cs-qty-' + i" type="number" inputmode="numeric" min="0" step="1" formControlName="quantity" />
                  <app-control-errors [control]="line.controls.quantity" label="Quantity" />
                </div>
                <div class="form-field">
                  <button type="button" class="button button--small" (click)="removeLine(i)">Remove this line</button>
                </div>
              </div>
            }
          </div>

          <div class="form-actions">
            <button type="button" class="button" (click)="addLine()">Add a line</button>
            <button type="submit" class="button button--primary" [disabled]="submitting()">{{ submitting() ? 'Saving…' : 'Save counts for this bin' }}</button>
          </div>
        </form>
      }

      @if (count().lines.length > 0) {
        <h3>Counted so far</h3>
        <table class="responsive">
          <caption class="sr-only">Counted so far</caption>
          <thead>
            <tr>
              <th scope="col">Bin</th>
              <th scope="col">SKU</th>
              <th scope="col">Grade</th>
              <th scope="col">Counted</th>
            </tr>
          </thead>
          <tbody>
            @for (line of count().lines; track line.binId + '-' + line.variantRefId + '-' + line.gradeId) {
              <tr>
                <td data-label="Bin">{{ line.binCode }}</td>
                <td data-label="SKU">{{ line.skuCode }}</td>
                <td data-label="Grade">{{ line.gradeCode === 'CONCESSION' ? 'Concession' : 'Accepted' }}</td>
                <td data-label="Counted">{{ line.countedQuantity }}</td>
              </tr>
            }
          </tbody>
        </table>
      }
    </section>
  `,
  styles: ':host { display: block; margin-bottom: var(--space-5); }',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CountSheetComponent implements OnInit {
  private readonly formBuilder = inject(FormBuilder);

  readonly count = input.required<CycleCountDto>();
  readonly submitting = input(false);
  readonly defaultPerson = input('');

  readonly submitted = output<SubmitLinesValue>();

  readonly grades = GRADES;
  readonly summary = signal<string | null>(null);

  readonly form = this.formBuilder.group({
    binCode: this.formBuilder.nonNullable.control('', Validators.required),
    actingPerson: this.formBuilder.nonNullable.control('', [Validators.required, Validators.minLength(2), Validators.maxLength(100)]),
    recount: this.formBuilder.nonNullable.control(false),
    lines: this.formBuilder.array([this.newLine()]),
  });

  get lines(): FormArray<ReturnType<CountSheetComponent['newLine']>> {
    return this.form.controls.lines;
  }

  ngOnInit(): void {
    this.form.patchValue({ actingPerson: this.defaultPerson() });
  }

  newLine() {
    return this.formBuilder.nonNullable.group({
      gtin: ['', [Validators.required, Validators.pattern(/^[0-9]{13}$/)]],
      gradeId: [1],
      quantity: [0, [Validators.required, Validators.min(0), Validators.max(100_000)]],
    });
  }

  addLine(): void {
    this.lines.push(this.newLine());
  }

  removeLine(index: number): void {
    this.lines.removeAt(index);
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.summary.set("This can't be saved yet. Choose the bin, give each line a 13-digit barcode and a whole quantity, and add your name.");
      return;
    }

    this.summary.set(null);
    const value = this.form.getRawValue();
    this.submitted.emit({
      binCode: value.binCode,
      lines: value.lines.map((l) => ({ gtin: l.gtin, gradeId: l.gradeId, quantity: Math.trunc(Number(l.quantity)) })),
      recount: value.recount,
      actingPerson: value.actingPerson.trim(),
    });
  }
}
