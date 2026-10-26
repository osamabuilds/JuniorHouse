import { ChangeDetectionStrategy, Component, effect, inject, input, output, untracked } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { LookupDto } from '@features/reference/lookups';
import { ControlErrorsComponent, inputChecked } from '@shared';
import { ProductContentValue, ProductDto } from '../../models';
import { HasPermissionDirective } from '@core/auth';

/**
 * The Content tab (PRD-5): name, description, who it is for, category, age group, and the collections it belongs to (PRD-12).
 * On a published product the gender and category are read-only here because they are part of the URL: they change on the URL tab.
 */
@Component({
  selector: 'app-product-content-form',
  imports: [HasPermissionDirective, ReactiveFormsModule, ControlErrorsComponent],
  template: `
    <form [formGroup]="form" (ngSubmit)="submit()" aria-label="Product content" novalidate>
      <div class="form-grid">
        <div class="form-field">
          <label for="pf-name">Product name</label>
          <input id="pf-name" type="text" formControlName="name" maxlength="200" />
          <app-control-errors [control]="form.controls.name" label="product name" />
        </div>
        <div class="form-field">
          <label for="pf-gender">For</label>
          <select id="pf-gender" formControlName="genderId">
            @for (gender of genders(); track gender.id) {
              <option [ngValue]="gender.id">{{ gender.name }}</option>
            }
          </select>
          @if (pathLocked()) {
            <small class="field-hint">Part of the URL, so it is changed on the URL tab.</small>
          }
        </div>
        <div class="form-field">
          <label for="pf-category">Category</label>
          <select id="pf-category" formControlName="categoryId">
            @for (category of categories(); track category.id) {
              <option [ngValue]="category.id">{{ category.name }}</option>
            }
          </select>
        </div>
        <div class="form-field">
          <label for="pf-age">Age group</label>
          <select id="pf-age" formControlName="ageBracketId">
            @for (age of ageBrackets(); track age.id) {
              <option [ngValue]="age.id">{{ age.name }}</option>
            }
          </select>
        </div>
      </div>
      <div class="form-field">
        <label for="pf-description">Description</label>
        <textarea id="pf-description" formControlName="description" rows="5" maxlength="2000"></textarea>
        <small class="field-hint">Plain text. Shown on the product page; no formatting.</small>
        <app-control-errors [control]="form.controls.description" label="description" />
      </div>
      <div class="form-actions">
        <button type="submit" class="button button--primary" [disabled]="disabled() || saving()">{{ saving() ? 'Saving…' : 'Save content' }}</button>
      </div>
    </form>

    <fieldset class="card" [disabled]="disabled() || saving()">
      <legend>Collections</legend>
      @for (collection of collections(); track collection.id) {
        <label class="checkbox">
          <input type="checkbox" [checked]="chosen().has(collection.id)" (change)="toggle(collection.id, $event)" />
          {{ collection.name }}
        </label>
      }
      <div class="form-actions">
        <button *hasPermission="'catalog.write'" type="button" class="button" (click)="collectionsSaved.emit([...chosen()])">Save collections</button>
      </div>
    </fieldset>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProductContentFormComponent {
  private readonly formBuilder = inject(FormBuilder);

  readonly product = input.required<ProductDto>();
  readonly genders = input.required<readonly LookupDto[]>();
  readonly categories = input.required<readonly LookupDto[]>();
  readonly ageBrackets = input.required<readonly LookupDto[]>();
  readonly collections = input.required<readonly LookupDto[]>();
  readonly saving = input(false);
  readonly disabled = input(false);

  readonly submitted = output<ProductContentValue>();
  readonly collectionsSaved = output<readonly number[]>();

  readonly form = this.formBuilder.group({
    name: this.formBuilder.nonNullable.control('', [Validators.required, Validators.maxLength(200)]),
    description: this.formBuilder.nonNullable.control('', Validators.maxLength(2000)),
    genderId: this.formBuilder.nonNullable.control(0),
    categoryId: this.formBuilder.nonNullable.control(0),
    ageBracketId: this.formBuilder.nonNullable.control(0),
  });

  private readonly selected = new Set<number>();
  chosen = () => this.selected;

  constructor() {
    effect(() => {
      const product = this.product();
      untracked(() => {
        this.form.reset({
          name: product.name,
          description: product.description,
          genderId: product.genderId,
          categoryId: product.categoryId,
          ageBracketId: product.ageBracketId,
        });
        this.selected.clear();
        product.collectionIds.forEach((id) => this.selected.add(id));
        this.syncLocks();
      });
    });
    effect(() => {
      this.disabled();
      untracked(() => this.syncLocks());
    });
  }

  pathLocked(): boolean {
    return this.product().status === 'Published';
  }

  toggle(id: number, event: Event): void {
    if (inputChecked(event)) {
      this.selected.add(id);
    } else {
      this.selected.delete(id);
    }
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();
    this.submitted.emit({
      name: value.name.trim(),
      description: value.description.trim(),
      genderId: value.genderId,
      categoryId: value.categoryId,
      ageBracketId: value.ageBracketId,
      careNote: this.product().careNote,
    });
  }

  private syncLocks(): void {
    if (this.disabled()) {
      this.form.disable({ emitEvent: false });
      return;
    }

    this.form.enable({ emitEvent: false });
    if (this.pathLocked()) {
      this.form.controls.genderId.disable({ emitEvent: false });
      this.form.controls.categoryId.disable({ emitEvent: false });
    }
  }
}
