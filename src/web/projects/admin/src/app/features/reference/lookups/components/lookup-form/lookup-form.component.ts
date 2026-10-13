import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { ReactiveFormsModule } from '@angular/forms';
import { ApiError } from '@core/http';
import { ControlErrorsComponent, FieldErrorsComponent, PageMode } from '@shared';
import { LookupFormGroup } from '../../forms/lookup.form';
import { LookupTypeConfig } from '../../models';

@Component({
  selector: 'app-lookup-form',
  imports: [ReactiveFormsModule, FieldErrorsComponent, ControlErrorsComponent],
  templateUrl: './lookup-form.component.html',
  styleUrl: './lookup-form.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LookupFormComponent {
  readonly form = input.required<LookupFormGroup>();
  readonly type = input.required<LookupTypeConfig>();
  readonly mode = input.required<PageMode>();
  readonly saving = input.required<boolean>();
  readonly error = input.required<ApiError | null>();

  readonly submitted = output<void>();
  readonly cancelled = output<void>();

  private readonly fieldErrorsByName = computed(() => this.error()?.fieldErrors ?? {});

  fieldErrors(field: string): readonly string[] {
    return this.fieldErrorsByName()[field] ?? [];
  }
}
