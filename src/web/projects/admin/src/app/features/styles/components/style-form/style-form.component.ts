import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { ReactiveFormsModule } from '@angular/forms';
import { ApiError } from '@core/http';
import { LookupDto } from '@features/reference-data';
import { ControlErrorsComponent, FieldErrorsComponent, inputChecked, inputNumber, PageMode } from '@shared';
import { StyleFormGroup } from '../../forms/style.form';
import { GridQtyChange, GridRow } from '../../models';

export interface OptionToggle {
  readonly id: number;
  readonly checked: boolean;
}

@Component({
  selector: 'app-style-form',
  imports: [ReactiveFormsModule, FieldErrorsComponent, ControlErrorsComponent],
  templateUrl: './style-form.component.html',
  styleUrl: './style-form.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StyleFormComponent {
  readonly form = input.required<StyleFormGroup>();
  readonly mode = input.required<PageMode>();
  readonly saving = input.required<boolean>();
  readonly error = input.required<ApiError | null>();
  readonly categories = input.required<readonly LookupDto[]>();
  readonly genders = input.required<readonly LookupDto[]>();
  readonly ageBrackets = input.required<readonly LookupDto[]>();
  readonly fabrics = input.required<readonly LookupDto[]>();
  readonly colours = input.required<readonly LookupDto[]>();
  readonly sizes = input.required<readonly LookupDto[]>();
  readonly selectedColourIds = input.required<readonly number[]>();
  readonly selectedSizeIds = input.required<readonly number[]>();
  readonly gridRows = input.required<readonly GridRow[]>();

  readonly submitted = output<void>();
  readonly cancelled = output<void>();
  readonly colourToggled = output<OptionToggle>();
  readonly sizeToggled = output<OptionToggle>();
  readonly qtyChanged = output<GridQtyChange>();

  private readonly fieldErrorsByName = computed(() => this.error()?.fieldErrors ?? {});

  fieldErrors(field: string): readonly string[] {
    return this.fieldErrorsByName()[field] ?? [];
  }

  colourName(colourId: number): string | undefined {
    return this.colours().find((colour) => colour.id === colourId)?.name;
  }

  onColourToggle(id: number, event: Event): void {
    this.colourToggled.emit({ id, checked: inputChecked(event) });
  }

  onSizeToggle(id: number, event: Event): void {
    this.sizeToggled.emit({ id, checked: inputChecked(event) });
  }

  onQtyInput(sizeId: number, colourId: number, event: Event): void {
    this.qtyChanged.emit({ sizeId, colourId, qty: inputNumber(event) });
  }
}
