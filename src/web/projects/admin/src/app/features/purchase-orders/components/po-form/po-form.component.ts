import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { ReactiveFormsModule } from '@angular/forms';
import { ApiError } from '@core/http';
import { LookupDto, LookupNameResolver } from '@features/reference-data';
import { StyleSummaryDto } from '@features/styles';
import { VendorSummaryDto } from '@features/vendors';
import { ControlErrorsComponent, FieldErrorsComponent, inputNumber, selectNumberOrNull } from '@shared';
import { PoFormGroup } from '../../forms/po.form';
import { LineQtyChange, LineRow, PoPageMode } from '../../models';

/** SCRUM-174: the raise / edit-draft form. It builds nothing and calls nothing: it emits what the user does. */
@Component({
  selector: 'app-po-form',
  imports: [ReactiveFormsModule, FieldErrorsComponent, ControlErrorsComponent],
  templateUrl: './po-form.component.html',
  styleUrl: './po-form.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PoFormComponent {
  readonly form = input.required<PoFormGroup>();
  readonly mode = input.required<PoPageMode>();
  readonly saving = input.required<boolean>();
  readonly error = input.required<ApiError | null>();
  readonly names = input.required<LookupNameResolver>();
  readonly vendors = input.required<readonly VendorSummaryDto[]>();
  readonly styles = input.required<readonly StyleSummaryDto[]>();
  readonly paymentTerms = input.required<readonly LookupDto[]>();
  readonly fabricOptions = input.required<readonly LookupDto[]>();
  readonly lineRows = input.required<readonly LineRow[]>();

  readonly submitted = output<void>();
  readonly cancelled = output<void>();
  readonly vendorChanged = output<number | null>();
  readonly styleChanged = output<number | null>();
  readonly lineQtyChanged = output<LineQtyChange>();

  private readonly fieldErrorsByName = computed(() => this.error()?.fieldErrors ?? {});

  fieldErrors(field: string): readonly string[] {
    return this.fieldErrorsByName()[field] ?? [];
  }

  onVendorSelectChange(event: Event): void {
    this.vendorChanged.emit(selectNumberOrNull(event));
  }

  onStyleSelectChange(event: Event): void {
    this.styleChanged.emit(selectNumberOrNull(event));
  }

  onLineQtyInput(sizeId: number, colourId: number, event: Event): void {
    this.lineQtyChanged.emit({ sizeId, colourId, qty: inputNumber(event) });
  }
}
