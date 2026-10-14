import { TestBed } from '@angular/core/testing';
import { lookup } from '@app/testing/catalog-fixtures';
import { textOf } from '@app/testing/production-fixtures';
import { ApiError } from '@core/http';
import { CreateProductValue } from '../../models';
import { ProductCreateFormComponent } from './product-create-form.component';

describe('ProductCreateFormComponent (spec PRD-1)', () => {
  function create(error: ApiError | null = null) {
    const fixture = TestBed.createComponent(ProductCreateFormComponent);
    fixture.componentRef.setInput('styles', [{ id: 3, code: 'STY-DRESS-001', name: 'Floral Summer Dress', categoryId: 3 }]);
    fixture.componentRef.setInput('genders', [lookup(2, 'Girls')]);
    fixture.componentRef.setInput('categories', [lookup(3, 'Dresses')]);
    fixture.componentRef.setInput('ageBrackets', [lookup(4, 'Kids')]);
    fixture.componentRef.setInput('error', error);
    fixture.componentRef.setInput('defaultPerson', 'Ayesha Khan');
    const submitted = vi.fn<(value: CreateProductValue) => void>();
    fixture.componentInstance.submitted.subscribe(submitted);
    fixture.detectChanges();
    const root = fixture.nativeElement as HTMLElement;
    return { fixture, root, submitted };
  }

  it('every field has a visible label, and the remembered name is filled in (NFR-19)', () => {
    const { root } = create();

    for (const id of ['pc-style', 'pc-name', 'pc-gender', 'pc-category', 'pc-age', 'pc-person', 'pc-description']) {
      expect(root.querySelector(`label[for="${id}"]`)).not.toBeNull();
    }
    expect((root.querySelector('#pc-person') as HTMLInputElement).value).toBe('Ayesha Khan');
  });

  it('choosing a style fills in its name and category', () => {
    const { fixture, root } = create();
    const select = root.querySelector('#pc-style') as HTMLSelectElement;

    fixture.componentInstance.form.controls.styleId.setValue(3);
    select.dispatchEvent(new Event('change'));
    fixture.detectChanges();

    expect((root.querySelector('#pc-name') as HTMLInputElement).value).toBe('Floral Summer Dress');
    expect(fixture.componentInstance.form.controls.categoryId.value).toBe(3);
  });

  it('will not send until a style, audience, category and age group are chosen', () => {
    const { fixture, root, submitted } = create();

    (root.querySelector('form') as HTMLFormElement).dispatchEvent(new Event('submit'));
    fixture.detectChanges();

    expect(submitted).not.toHaveBeenCalled();
    expect(textOf(fixture)).toContain("This can't be saved yet");
  });

  it('sends the chosen values, trimmed', () => {
    const { fixture, root, submitted } = create();
    fixture.componentInstance.form.patchValue({ styleId: 3, name: '  Floral Summer Dress ', genderId: 2, categoryId: 3, ageBracketId: 4, description: ' Soft. ' });

    (root.querySelector('form') as HTMLFormElement).dispatchEvent(new Event('submit'));

    expect(submitted).toHaveBeenCalledWith({
      styleId: 3,
      name: 'Floral Summer Dress',
      description: 'Soft.',
      genderId: 2,
      categoryId: 3,
      ageBracketId: 4,
      actingPerson: 'Ayesha Khan',
    });
  });

  it("shows the server's reason for a refused create (one product per style)", () => {
    const { fixture } = create({ status: 409, message: 'Style STY-DRESS-001 already has a product (product 5).', fieldErrors: {} });

    expect(textOf(fixture)).toContain('already has a product');
  });
});
