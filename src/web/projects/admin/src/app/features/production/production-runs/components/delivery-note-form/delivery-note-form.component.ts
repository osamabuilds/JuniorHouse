import { ChangeDetectionStrategy, Component, OnInit, computed, inject, input, output, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ApiError } from '@core/http';
import { LookupNameResolver } from '@features/reference/lookups';
import { ControlErrorsComponent, FieldErrorsComponent, inputNumber } from '@shared';
import { DeliveryNoteValue, OrderedVsShippedDto } from '../../models';
import { todayInPakistan } from '../../utils';

/**
 * Records a vendor delivery note (spec AC-18..AC-24, D3, D4). The quantity grid lists what was ordered and what has
 * shipped already, so the person can see at a glance whether this note completes the order. Extra pieces beyond the
 * allowance are still recorded (and flagged); the "final shipment" box says the vendor will send nothing more, which is
 * what lets a short order count as delivered.
 */
@Component({
  selector: 'app-delivery-note-form',
  imports: [ReactiveFormsModule, ControlErrorsComponent, FieldErrorsComponent],
  template: `
    <form class="card" [formGroup]="form" (ngSubmit)="submit()" aria-labelledby="dn-heading" novalidate>
      <h3 id="dn-heading">Record a delivery note</h3>
      <p class="field-hint">Copy the vendor's delivery note. A purchase order can have several: one for each shipment.</p>

      @if (error()) {
        <p class="banner banner--error" role="alert">{{ error()?.message }}</p>
      }
      @if (summary()) {
        <p class="banner banner--error" role="alert">{{ summary() }}</p>
      }

      <div class="form-grid">
        <div class="form-field">
          <label for="dn-no">Vendor's delivery note number</label>
          <input id="dn-no" type="text" formControlName="vendorDnNo" maxlength="50" />
          <app-control-errors [control]="form.controls.vendorDnNo" label="Delivery note number" />
          <app-field-errors [messages]="fieldErrors('VendorDnNo')" />
        </div>

        <div class="form-field">
          <label for="dn-date">Date it was dispatched</label>
          <input id="dn-date" type="date" formControlName="dispatchDate" [min]="minDate()" [max]="today" />
          <p class="field-hint">Judged against the terms in force on this day, not the day you enter it.</p>
          <app-control-errors [control]="form.controls.dispatchDate" label="Dispatch date" />
          <app-field-errors [messages]="fieldErrors('DispatchDate')" />
        </div>

        <div class="form-field">
          <label for="dn-person">Your name</label>
          <input id="dn-person" type="text" formControlName="actingPerson" maxlength="100" autocomplete="name" />
          <app-control-errors [control]="form.controls.actingPerson" label="Your name" />
          <app-field-errors [messages]="fieldErrors('ActingPerson')" />
        </div>

        <div class="form-field">
          <label class="check" for="dn-final">
            <input id="dn-final" type="checkbox" formControlName="isFinalShipment" />
            This is the vendor's final shipment
          </label>
          <p class="field-hint">Tick this if nothing more will be sent, even if the order is short.</p>
        </div>
      </div>

      <fieldset>
        <legend>Pieces in this shipment</legend>
        <table class="responsive line-grid">
          <caption class="sr-only">
            Pieces in this shipment
          </caption>
          <thead>
            <tr>
              <th scope="col">Size</th>
              <th scope="col">Colour</th>
              <th scope="col">Ordered</th>
              <th scope="col">Sent before</th>
              <th scope="col">In this shipment</th>
            </tr>
          </thead>
          <tbody>
            @for (row of quantities(); track row.sizeId + '-' + row.colourId) {
              <tr>
                <td data-label="Size">{{ names().sizeName(row.sizeId) }}</td>
                <td data-label="Colour">{{ names().colourName(row.colourId) }}</td>
                <td data-label="Ordered">{{ row.ordered }}</td>
                <td data-label="Sent before">{{ row.shipped }}</td>
                <td data-label="In this shipment">
                  <label class="sr-only" [for]="'dn-qty-' + row.sizeId + '-' + row.colourId">
                    Pieces of {{ names().sizeName(row.sizeId) }} {{ names().colourName(row.colourId) }} in this shipment
                  </label>
                  <input
                    [id]="'dn-qty-' + row.sizeId + '-' + row.colourId"
                    type="number"
                    min="0"
                    step="1"
                    [value]="qtyOf(row)"
                    (input)="onQty(row.sizeId, row.colourId, $event)"
                  />
                </td>
              </tr>
            }
          </tbody>
        </table>
        <p class="field-hint">Enter 0 (or leave blank) for a size and colour that is not in this shipment.</p>
        <app-field-errors [messages]="fieldErrors('Lines')" />
      </fieldset>

      <div class="form-actions">
        <button type="submit" class="button button--primary" [disabled]="saving()">{{ saving() ? 'Saving…' : 'Record delivery note' }}</button>
        <button type="button" class="button" (click)="cancelled.emit()">Cancel</button>
      </div>
    </form>
  `,
  styles: ':host { display: block; margin-bottom: var(--space-5); }',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DeliveryNoteFormComponent implements OnInit {
  private readonly formBuilder = inject(FormBuilder);

  readonly quantities = input.required<readonly OrderedVsShippedDto[]>();
  readonly names = input.required<LookupNameResolver>();
  /** The day the PO was sent: no delivery can be dated before it. */
  readonly minDate = input.required<string>();
  readonly saving = input(false);
  readonly error = input<ApiError | null>(null);
  readonly defaultPerson = input('');

  readonly submitted = output<DeliveryNoteValue>();
  readonly cancelled = output<void>();

  readonly today = todayInPakistan();
  readonly summary = signal<string | null>(null);
  private readonly qtyByCell = signal<Readonly<Record<string, number>>>({});
  readonly totalPieces = computed(() => Object.values(this.qtyByCell()).reduce((sum, qty) => sum + (qty > 0 ? qty : 0), 0));

  readonly form = this.formBuilder.group({
    vendorDnNo: this.formBuilder.nonNullable.control('', [Validators.required, Validators.maxLength(50)]),
    dispatchDate: this.formBuilder.nonNullable.control(this.today, Validators.required),
    isFinalShipment: this.formBuilder.nonNullable.control(false),
    actingPerson: this.formBuilder.nonNullable.control('', [Validators.required, Validators.minLength(2), Validators.maxLength(100)]),
  });

  ngOnInit(): void {
    this.form.patchValue({ actingPerson: this.defaultPerson() });
  }

  fieldErrors(field: string): readonly string[] {
    return this.error()?.fieldErrors[field] ?? [];
  }

  qtyOf(row: OrderedVsShippedDto): number {
    return this.qtyByCell()[`${row.sizeId}-${row.colourId}`] ?? 0;
  }

  onQty(sizeId: number, colourId: number, event: Event): void {
    this.qtyByCell.set({ ...this.qtyByCell(), [`${sizeId}-${colourId}`]: Math.max(0, Math.floor(inputNumber(event) || 0)) });
  }

  submit(): void {
    const missing: string[] = [];
    const controls = this.form.controls;
    if (controls.vendorDnNo.invalid) {
      missing.push('the delivery note number');
    }
    if (controls.dispatchDate.invalid) {
      missing.push('the dispatch date');
    }
    if (controls.actingPerson.invalid) {
      missing.push('your name');
    }
    if (this.totalPieces() === 0) {
      missing.push('at least one piece in the shipment');
    }

    if (missing.length > 0) {
      this.form.markAllAsTouched();
      this.summary.set(`This can't be saved yet. Still needed: ${missing.join(', ')}.`);
      return;
    }

    this.summary.set(null);
    const raw = this.form.getRawValue();
    this.submitted.emit({
      vendorDnNo: raw.vendorDnNo.trim(),
      dispatchDate: raw.dispatchDate,
      isFinalShipment: raw.isFinalShipment,
      actingPerson: raw.actingPerson.trim(),
      lines: this.quantities()
        .map((row) => ({ sizeId: row.sizeId, colourId: row.colourId, qty: this.qtyOf(row) }))
        .filter((line) => line.qty > 0),
    });
  }
}
