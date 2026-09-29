import { Component, OnInit, computed, inject, input, output, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ApiError } from '@core/http';
import { LookupDto, LookupNameResolver } from '@features/reference-data';
import { FieldErrorsComponent, inputChecked, inputNumber, selectNumberOrNull } from '@shared';
import { AmendmentFileAdd, AmendmentValue, PoDto, PoFileDto, StyleCell } from '../../models';
import { FILE_CATEGORY_LABELS } from '../../purchase-orders.constants';
import { fileToBase64 } from '../../utils/file-to-base64.util';
import { isVendorVisibleCategory } from '../../utils/po-file-category.util';

const cellKey = (sizeId: number, colourId: number): string => `${sizeId}-${colourId}`;

/**
 * SCRUM-93 task 48 (AC-9..AC-17, AC-38): the amendment form. Any subset of the terms, the size
 * &times; colour quantities and the vendor-visible files can change; a reason and an internal note
 * are mandatory, a message to the vendor is optional. In `counter` mode (task 49) it captures the
 * vendor's counter-proposal instead - same fields, initiated by the vendor, no file changes.
 */
@Component({
  selector: 'app-amend-form',
  imports: [ReactiveFormsModule, FieldErrorsComponent],
  template: `
    <form class="amend card" [formGroup]="form" (ngSubmit)="submit()" [attr.aria-labelledby]="'amend-heading'">
      <h3 id="amend-heading">{{ mode() === 'counter' ? 'Counter-proposal from the vendor' : 'Amend this PO' }}</h3>
      <p class="field-hint">
        Change only what is different; everything else stays as it is now. If nothing changes, the amendment is not accepted.
      </p>

      @if (error()) {
        <p class="banner banner--error" role="alert">{{ error()?.message }}</p>
      }

      <div class="form-grid">
        <div class="form-field">
          <label for="amend-reason">Reason</label>
          <select id="amend-reason" formControlName="reasonId">
            <option [ngValue]="null">Select a reason…</option>
            @for (reason of reasons(); track reason.id) {
              <option [ngValue]="reason.id">{{ reason.name }}</option>
            }
          </select>
          @if (submitted() && form.controls.reasonId.invalid) {
            <p class="field-error" role="alert">Choose a reason for this change.</p>
          }
          <app-field-errors [messages]="fieldErrors('ReasonId')" />
        </div>

        <div class="form-field">
          <label for="amend-unit-cost">Unit cost (PKR)</label>
          <input id="amend-unit-cost" type="number" step="0.01" formControlName="unitCost" />
          <app-field-errors [messages]="fieldErrors('UnitCost')" />
        </div>

        <div class="form-field">
          <label for="amend-delivery">Expected delivery date</label>
          <input id="amend-delivery" type="date" formControlName="expectedDeliveryDate" />
          <app-field-errors [messages]="fieldErrors('ExpectedDeliveryDate')" />
        </div>

        <div class="form-field">
          <label for="amend-latest">Latest acceptable delivery date</label>
          <input id="amend-latest" type="date" formControlName="latestAcceptableDate" />
          <app-field-errors [messages]="fieldErrors('LatestAcceptableDate')" />
        </div>

        <div class="form-field">
          <label for="amend-over">Extra pieces allowed (%)</label>
          <input id="amend-over" type="number" step="0.01" formControlName="overTolerancePercent" />
          <app-field-errors [messages]="fieldErrors('OverTolerancePercent')" />
        </div>

        <div class="form-field">
          <label for="amend-under">Fewer pieces allowed (%)</label>
          <input id="amend-under" type="number" step="0.01" formControlName="underTolerancePercent" />
          <app-field-errors [messages]="fieldErrors('UnderTolerancePercent')" />
        </div>

        <div class="form-field">
          <label for="amend-advance">Advance payment (%)</label>
          <input id="amend-advance" type="number" step="0.01" formControlName="advancePercent" />
        </div>

        <div class="form-field">
          <label for="amend-fabric">Who supplies the fabric</label>
          <select id="amend-fabric" formControlName="fabricResponsibilityId">
            <option [ngValue]="null">Not set</option>
            @for (option of fabricOptions(); track option.id) {
              <option [ngValue]="option.id">{{ option.name }}</option>
            }
          </select>
        </div>

        <div class="form-field form-field--wide">
          <label for="amend-note">Internal note: why this change, and what it costs (the vendor never sees this)</label>
          <textarea id="amend-note" rows="2" formControlName="impactNote"></textarea>
          @if (submitted() && form.controls.impactNote.invalid) {
            <p class="field-error" role="alert">Explain the impact of this change for the record.</p>
          }
          <app-field-errors [messages]="fieldErrors('ImpactNote')" />
        </div>

        <div class="form-field form-field--wide">
          <label for="amend-message">Message to the vendor (optional)</label>
          <textarea id="amend-message" rows="2" formControlName="vendorMessage"></textarea>
        </div>
      </div>

      <fieldset class="amend__lines">
        <legend>Quantity for each size and colour (enter 0 to remove one)</legend>
        <table class="line-grid">
          <thead>
            <tr>
              <th scope="col">Size</th>
              <th scope="col">Colour</th>
              <th scope="col">Quantity</th>
            </tr>
          </thead>
          <tbody>
            @for (row of lineRows(); track row.sizeId + '-' + row.colourId) {
              <tr>
                <th scope="row">{{ names().sizeName(row.sizeId) }}</th>
                <td>{{ names().colourName(row.colourId) }}</td>
                <td>
                  <input
                    type="number"
                    min="0"
                    [attr.aria-label]="'Quantity for ' + names().sizeName(row.sizeId) + ', ' + names().colourName(row.colourId)"
                    [value]="row.qty"
                    (input)="onQty(row.sizeId, row.colourId, $event)"
                  />
                </td>
              </tr>
            }
          </tbody>
        </table>
        <app-field-errors [messages]="fieldErrors('Lines')" />
      </fieldset>

      @if (mode() === 'amend') {
        <fieldset class="amend__files">
          <legend>Vendor-visible files</legend>
          @if (effectiveFiles().length > 0) {
            <ul>
              @for (file of effectiveFiles(); track file.id) {
                <li>
                  <label>
                    <input type="checkbox" [checked]="retired().has(file.id)" (change)="onRetire(file.id, $event)" />
                    Retire {{ file.fileName }} ({{ categoryLabel(file.categoryId) }})
                  </label>
                </li>
              }
            </ul>
          } @else {
            <p class="field-hint">No vendor-visible files are in effect.</p>
          }

          <div class="form-field">
            <label for="amend-file-category">Add a file: category</label>
            <select id="amend-file-category" [value]="newFileCategory()" (change)="onFileCategory($event)">
              @for (category of vendorVisibleCategories; track category.id) {
                <option [value]="category.id">{{ category.name }}</option>
              }
            </select>
          </div>
          <div class="form-field">
            <label for="amend-file">Add a file: choose file (PDF, PNG, JPEG, Excel or Word)</label>
            <input id="amend-file" type="file" (change)="onFilePicked($event)" />
          </div>
          @if (addedFiles().length > 0) {
            <ul>
              @for (file of addedFiles(); track file.fileName) {
                <li>
                  Adding {{ file.fileName }} ({{ categoryLabel(file.categoryId) }})
                  <button type="button" class="button button--small" (click)="removeAdded(file.fileName)">Remove</button>
                </li>
              }
            </ul>
          }
          <app-field-errors [messages]="fieldErrors('AddFiles')" />
          <app-field-errors [messages]="fieldErrors('RetireFileIds')" />
        </fieldset>
      }

      <div class="form-actions">
        <button type="submit" class="button button--primary" [disabled]="saving()">
          {{ saving() ? 'Saving…' : mode() === 'counter' ? 'Record counter-proposal' : 'Submit amendment' }}
        </button>
        <button type="button" class="button" (click)="cancelled.emit()">Cancel</button>
      </div>
    </form>
  `,
  styles: `
    .amend {
      margin-bottom: var(--space-5);
    }
    fieldset {
      border: 1px solid var(--color-border);
      border-radius: var(--radius-md);
      margin: var(--space-4) 0;
    }
    .field-error {
      color: var(--color-danger);
      font-size: 0.8125rem;
      margin: var(--space-1) 0 0;
    }
  `,
})
export class AmendFormComponent implements OnInit {
  private readonly formBuilder = inject(FormBuilder);

  readonly names = input.required<LookupNameResolver>();
  readonly po = input.required<PoDto>();
  readonly initiatorId = input(1);
  readonly mode = input<'amend' | 'counter'>('amend');
  readonly cells = input<readonly StyleCell[]>([]);
  readonly reasons = input<readonly LookupDto[]>([]);
  readonly fabricOptions = input<readonly LookupDto[]>([]);
  readonly files = input<readonly PoFileDto[]>([]);
  readonly error = input<ApiError | null>(null);
  readonly saving = input(false);

  readonly amended = output<AmendmentValue>();
  readonly cancelled = output<void>();

  readonly submitted = signal(false);
  private readonly qtyByCell = signal<ReadonlyMap<string, number>>(new Map());
  readonly retired = signal<ReadonlySet<number>>(new Set());
  readonly addedFiles = signal<readonly AmendmentFileAdd[]>([]);
  readonly newFileCategory = signal(1);

  readonly vendorVisibleCategories = Object.entries(FILE_CATEGORY_LABELS)
    .filter(([id]) => isVendorVisibleCategory(+id))
    .map(([id, name]) => ({ id: +id, name }));

  readonly effectiveFiles = computed(() => this.files().filter((file) => file.isVendorVisible && file.retiredInRevisionNumber === null));

  /** The style's full size × colour grid, plus any PO line outside it, so nothing the PO holds is hidden. */
  readonly lineRows = computed(() => {
    const quantities = this.qtyByCell();
    const known = new Map<string, StyleCell>();
    for (const cell of this.cells()) {
      known.set(cellKey(cell.sizeId, cell.colourId), cell);
    }
    for (const line of this.po().lines) {
      known.set(cellKey(line.sizeId, line.colourId), line);
    }
    return [...known.values()].map((cell) => ({
      sizeId: cell.sizeId,
      colourId: cell.colourId,
      qty: quantities.get(cellKey(cell.sizeId, cell.colourId)) ?? 0,
    }));
  });

  readonly form = this.formBuilder.group({
    reasonId: this.formBuilder.control<number | null>(null, Validators.required),
    impactNote: this.formBuilder.nonNullable.control('', Validators.required),
    vendorMessage: this.formBuilder.nonNullable.control(''),
    unitCost: this.formBuilder.nonNullable.control(0, [Validators.required, Validators.min(0.01)]),
    expectedDeliveryDate: this.formBuilder.nonNullable.control('', Validators.required),
    latestAcceptableDate: this.formBuilder.nonNullable.control(''),
    overTolerancePercent: this.formBuilder.control<number | null>(null),
    underTolerancePercent: this.formBuilder.control<number | null>(null),
    advancePercent: this.formBuilder.nonNullable.control(0),
    fabricResponsibilityId: this.formBuilder.control<number | null>(null),
  });

  ngOnInit(): void {
    const po = this.po();
    this.form.patchValue({
      unitCost: po.unitCost,
      expectedDeliveryDate: po.expectedDeliveryDate,
      latestAcceptableDate: po.latestAcceptableDate ?? '',
      overTolerancePercent: po.overTolerancePercent,
      underTolerancePercent: po.underTolerancePercent,
      advancePercent: po.advancePercent,
      fabricResponsibilityId: po.fabricResponsibilityId,
    });
    this.qtyByCell.set(new Map(po.lines.map((line) => [cellKey(line.sizeId, line.colourId), line.qty])));
  }

  fieldErrors(field: string): readonly string[] {
    return this.error()?.fieldErrors[field] ?? [];
  }

  categoryLabel(categoryId: number): string {
    return FILE_CATEGORY_LABELS[categoryId] ?? `#${categoryId}`;
  }


  onQty(sizeId: number, colourId: number, event: Event): void {
    const next = new Map(this.qtyByCell());
    next.set(cellKey(sizeId, colourId), inputNumber(event));
    this.qtyByCell.set(next);
  }

  onRetire(fileId: number, event: Event): void {
    const next = new Set(this.retired());
    if (inputChecked(event)) {
      next.add(fileId);
    } else {
      next.delete(fileId);
    }
    this.retired.set(next);
  }

  onFileCategory(event: Event): void {
    this.newFileCategory.set(selectNumberOrNull(event) ?? 1);
  }

  async onFilePicked(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) {
      return;
    }
    const content = await fileToBase64(file);
    this.addedFiles.set([...this.addedFiles(), { categoryId: this.newFileCategory(), fileName: file.name, content }]);
    input.value = '';
  }

  removeAdded(fileName: string): void {
    this.addedFiles.set(this.addedFiles().filter((file) => file.fileName !== fileName));
  }

  submit(): void {
    this.submitted.set(true);
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const raw = this.form.getRawValue();
    const lines = this.lineRows()
      .filter((row) => row.qty > 0)
      .map((row) => ({ sizeId: row.sizeId, colourId: row.colourId, qty: row.qty }));
    const po = this.po();

    this.amended.emit({
      initiatorId: this.initiatorId(),
      reasonId: raw.reasonId!,
      impactNote: raw.impactNote.trim(),
      vendorMessage: raw.vendorMessage.trim() || null,
      unitCost: raw.unitCost,
      expectedDeliveryDate: raw.expectedDeliveryDate,
      latestAcceptableDate: raw.latestAcceptableDate || null,
      overTolerancePercent: raw.overTolerancePercent,
      underTolerancePercent: raw.underTolerancePercent,
      paymentTermId: po.paymentTermId,
      advancePercent: raw.advancePercent,
      fabricResponsibilityId: raw.fabricResponsibilityId,
      lines,
      addFiles: this.mode() === 'amend' ? this.addedFiles() : [],
      retireFileIds: this.mode() === 'amend' ? [...this.retired()] : [],
    });
  }
}
