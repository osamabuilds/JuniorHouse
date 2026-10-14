import { ChangeDetectionStrategy, Component, OnInit, inject, input, output, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ApiError } from '@core/http';
import { LookupDto } from '@features/reference/lookups';
import { ControlErrorsComponent, FieldErrorsComponent } from '@shared';
import { CreateProductValue } from '../../models';

/** A style that can get a product: the form needs only its id, code and name. */
export interface StyleChoice {
  readonly id: number;
  readonly code: string;
  readonly name: string;
  readonly categoryId: number;
}

/**
 * Creates a product from a style (PRD-1, D1): the planned variants are the style's colours and sizes; SKUs and GTINs come from
 * Labelling, never from here. The server checks the style, gives it a unique URL name and refuses a second product for the same style.
 */
@Component({
  selector: 'app-product-create-form',
  imports: [ReactiveFormsModule, ControlErrorsComponent, FieldErrorsComponent],
  template: `
    <form class="card" [formGroup]="form" (ngSubmit)="submit()" aria-labelledby="pc-heading" novalidate>
      <h2 id="pc-heading">Create a product from a style</h2>
      <p class="field-hint">The product starts as a draft. Its sizes and colours are the style's; barcodes arrive from Labelling as stock is labelled.</p>

      @if (error()) {
        <p class="banner banner--error" role="alert">{{ error()?.message }}</p>
      }
      @if (summary()) {
        <p class="banner banner--error" role="alert">{{ summary() }}</p>
      }

      <div class="form-grid">
        <div class="form-field">
          <label for="pc-style">Style</label>
          <select id="pc-style" formControlName="styleId" (change)="onStyleChosen()">
            <option [ngValue]="null">Choose a style</option>
            @for (style of styles(); track style.id) {
              <option [ngValue]="style.id">{{ style.code }} · {{ style.name }}</option>
            }
          </select>
          <app-control-errors [control]="form.controls.styleId" label="style" kind="choose" />
          <app-field-errors [messages]="fieldErrors('StyleId')" />
        </div>
        <div class="form-field">
          <label for="pc-name">Product name</label>
          <input id="pc-name" type="text" formControlName="name" maxlength="200" autocomplete="off" />
          <app-control-errors [control]="form.controls.name" label="product name" />
          <app-field-errors [messages]="fieldErrors('Name')" />
        </div>
        <div class="form-field">
          <label for="pc-gender">For</label>
          <select id="pc-gender" formControlName="genderId">
            <option [ngValue]="null">Choose</option>
            @for (gender of genders(); track gender.id) {
              <option [ngValue]="gender.id">{{ gender.name }}</option>
            }
          </select>
          <app-control-errors [control]="form.controls.genderId" label="audience" kind="choose" />
          <app-field-errors [messages]="fieldErrors('GenderId')" />
        </div>
        <div class="form-field">
          <label for="pc-category">Category</label>
          <select id="pc-category" formControlName="categoryId">
            <option [ngValue]="null">Choose</option>
            @for (category of categories(); track category.id) {
              <option [ngValue]="category.id">{{ category.name }}</option>
            }
          </select>
          <app-control-errors [control]="form.controls.categoryId" label="category" kind="choose" />
          <app-field-errors [messages]="fieldErrors('CategoryId')" />
        </div>
        <div class="form-field">
          <label for="pc-age">Age group</label>
          <select id="pc-age" formControlName="ageBracketId">
            <option [ngValue]="null">Choose</option>
            @for (age of ageBrackets(); track age.id) {
              <option [ngValue]="age.id">{{ age.name }}</option>
            }
          </select>
          <app-control-errors [control]="form.controls.ageBracketId" label="age group" kind="choose" />
          <app-field-errors [messages]="fieldErrors('AgeBracketId')" />
        </div>
        <div class="form-field">
          <label for="pc-person">Your name</label>
          <input id="pc-person" type="text" formControlName="actingPerson" maxlength="100" autocomplete="name" />
          <app-control-errors [control]="form.controls.actingPerson" label="your name" />
          <app-field-errors [messages]="fieldErrors('ActingPerson')" />
        </div>
      </div>
      <div class="form-field">
        <label for="pc-description">Description</label>
        <textarea id="pc-description" formControlName="description" rows="3" maxlength="2000"></textarea>
        <app-field-errors [messages]="fieldErrors('Description')" />
      </div>

      <div class="form-actions">
        <button type="submit" class="button button--primary" [disabled]="saving()">{{ saving() ? 'Creating…' : 'Create product' }}</button>
        <button type="button" class="button" (click)="cancelled.emit()">Cancel</button>
      </div>
    </form>
  `,
  styles: ':host { display: block; margin-bottom: var(--space-5); }',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProductCreateFormComponent implements OnInit {
  private readonly formBuilder = inject(FormBuilder);

  readonly styles = input.required<readonly StyleChoice[]>();
  readonly genders = input.required<readonly LookupDto[]>();
  readonly categories = input.required<readonly LookupDto[]>();
  readonly ageBrackets = input.required<readonly LookupDto[]>();
  readonly saving = input(false);
  readonly error = input<ApiError | null>(null);
  readonly defaultPerson = input('');

  readonly submitted = output<CreateProductValue>();
  readonly cancelled = output<void>();

  readonly summary = signal<string | null>(null);

  readonly form = this.formBuilder.group({
    styleId: this.formBuilder.control<number | null>(null, Validators.required),
    name: this.formBuilder.nonNullable.control('', [Validators.required, Validators.maxLength(200)]),
    genderId: this.formBuilder.control<number | null>(null, Validators.required),
    categoryId: this.formBuilder.control<number | null>(null, Validators.required),
    ageBracketId: this.formBuilder.control<number | null>(null, Validators.required),
    description: this.formBuilder.nonNullable.control('', Validators.maxLength(2000)),
    actingPerson: this.formBuilder.nonNullable.control('', [Validators.required, Validators.minLength(2), Validators.maxLength(100)]),
  });

  ngOnInit(): void {
    this.form.patchValue({ actingPerson: this.defaultPerson() });
  }

  fieldErrors(field: string): readonly string[] {
    return this.error()?.fieldErrors[field] ?? [];
  }

  /** Choosing a style fills in what it already says: the name and the category. */
  onStyleChosen(): void {
    const style = this.styles().find((candidate) => candidate.id === this.form.controls.styleId.value);
    if (style) {
      this.form.patchValue({ name: this.form.controls.name.value || style.name, categoryId: style.categoryId });
    }
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.summary.set("This can't be saved yet. Check the fields marked above.");
      return;
    }

    this.summary.set(null);
    const value = this.form.getRawValue();
    this.submitted.emit({
      styleId: value.styleId!,
      name: value.name.trim(),
      description: value.description.trim(),
      genderId: value.genderId!,
      categoryId: value.categoryId!,
      ageBracketId: value.ageBracketId!,
      actingPerson: value.actingPerson.trim(),
    });
  }
}
