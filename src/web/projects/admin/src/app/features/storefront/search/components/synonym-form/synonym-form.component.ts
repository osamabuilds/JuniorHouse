import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { ReactiveFormsModule } from '@angular/forms';
import { ApiError } from '@core/http';
import { ControlErrorsComponent, FieldErrorsComponent, PageMode } from '@shared';
import { SynonymFormGroup } from '../../forms/synonym.form';

/** Add or change a group of words that mean the same to a shopper (SRC-12). The same words work for every visitor within a minute. */
@Component({
  selector: 'app-synonym-form',
  imports: [ReactiveFormsModule, FieldErrorsComponent, ControlErrorsComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: ':host { display: block; } .card { max-width: 40rem; } textarea { width: 100%; min-height: 9rem; }',
  template: `
    <form class="card" (ngSubmit)="submitted.emit()" [formGroup]="form()">
      <h2>{{ mode() === 'create' ? 'Add' : 'Edit' }} synonym group</h2>
      <p class="page-intro">
        Words a shopper may use for the same thing. A search for any one of them also finds the others, for example frock, dress and gown.
        A word can belong to one group only.
      </p>

      @if (error()) {
        <p class="banner banner--error" role="alert">{{ error()?.message }}</p>
      }

      <div class="form-field">
        <label for="synonym-name">Name</label>
        <input id="synonym-name" type="text" formControlName="name" maxlength="60" />
        <app-control-errors [control]="form().controls.name" label="Name" kind="enter" />
        <app-field-errors [messages]="fieldErrors('name')" />
      </div>

      <div class="form-field">
        <label for="synonym-terms">Words, one per line</label>
        <textarea id="synonym-terms" formControlName="terms" rows="6" aria-describedby="synonym-terms-hint"></textarea>
        <p id="synonym-terms-hint" class="hint">Two to twenty words or short phrases of up to three words, for example shalwar kameez.</p>
        <app-control-errors [control]="form().controls.terms" label="Words" kind="enter" />
        <app-field-errors [messages]="fieldErrors('terms')" />
      </div>

      <div class="form-field">
        <label class="toolbar__checkbox"><input type="checkbox" formControlName="isActive" /> Use this group in searches</label>
      </div>

      <div class="form-actions">
        <button type="submit" class="button button--primary" [disabled]="saving()">{{ saving() ? 'Saving…' : 'Save' }}</button>
        <button type="button" class="button" (click)="cancelled.emit()">Cancel</button>
      </div>
    </form>
  `,
})
export class SynonymFormComponent {
  readonly form = input.required<SynonymFormGroup>();
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
