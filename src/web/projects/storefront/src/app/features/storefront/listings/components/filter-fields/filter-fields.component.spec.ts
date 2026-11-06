import { TestBed } from '@angular/core/testing';
import { ListingOptions } from '../../models/listing.model';
import { EMPTY_FILTER, ListingFilter } from '../../models/listing-query';
import { FilterFieldsComponent, FilterGroup } from './filter-fields.component';

const options: ListingOptions = {
  sizes: [{ value: '5-6y', label: '5-6 Years' }, { value: '7-8y', label: '7-8 Years' }],
  colours: [{ value: 'red', label: 'Red' }, { value: 'navy', label: 'Navy' }],
  priceMin: 500,
  priceMax: 4000,
  ages: [{ value: 'toddler', label: 'Toddler' }],
};

function render(over: { filter?: ListingFilter; group?: FilterGroup | null; options?: ListingOptions } = {}): HTMLElement {
  const f = TestBed.createComponent(FilterFieldsComponent);
  f.componentRef.setInput('options', over.options ?? options);
  f.componentRef.setInput('filter', over.filter ?? EMPTY_FILTER);
  f.componentRef.setInput('group', over.group ?? null);
  f.componentRef.setInput('defaultSortLabel', 'Newest');
  f.detectChanges();
  return f.nativeElement as HTMLElement;
}

describe('FilterFieldsComponent', () => {
  it('FLT-6 every option is a real form control with the names the listing already reads', () => {
    const el = render();
    expect([...el.querySelectorAll<HTMLInputElement>('input[name="size"]')].map((i) => i.value)).toEqual(['5-6y', '7-8y']);
    expect([...el.querySelectorAll<HTMLInputElement>('input[name="colour"]')].map((i) => i.value)).toEqual(['red', 'navy']);
    expect(el.querySelector<HTMLInputElement>('input[name="age"]')!.type).toBe('radio');
    expect(el.querySelector('input[name="priceMin"]')).not.toBeNull();
    expect(el.querySelector('input[name="priceMax"]')).not.toBeNull();
    expect(el.querySelector('select[name="sort"]')).not.toBeNull();
  });

  it('FLT-4 each colour is a round swatch with its name in words and a visible selected state, never colour alone', () => {
    const el = render({ filter: { ...EMPTY_FILTER, colours: ['navy'] } });
    const labels = [...el.querySelectorAll('label.rp-swatchbox')];
    expect(labels.length).toBe(2);
    expect(labels[0].textContent).toContain('Red');
    expect(labels[0].querySelector('.rp-swatch')!.getAttribute('aria-hidden')).toBe('true');
    expect(labels[0].querySelector('.rp-swatch')!.textContent!.trim()).toBe('R');
    expect(labels[1].querySelector<HTMLInputElement>('input')!.checked).toBe(true);
    expect(labels[0].querySelector<HTMLInputElement>('input')!.checked).toBe(false);
  });

  it('FLT-2 a single group renders only that group', () => {
    const el = render({ group: 'size' });
    expect(el.querySelectorAll('fieldset').length).toBe(1);
    expect(el.querySelector('legend')!.textContent).toContain('Size');
    expect(el.querySelector('input[name="colour"]')).toBeNull();
  });

  it('a group with no options is left out', () => {
    const el = render({ options: { ...options, colours: [], ages: [], priceMin: null, priceMax: null } });
    expect([...el.querySelectorAll('legend')].map((l) => l.textContent!.trim())).toEqual(['Size', 'Sort']);
  });

  it('the chosen values are checked and the sort is the current one', () => {
    const el = render({ filter: { ...EMPTY_FILTER, sizes: ['7-8y'], age: 'toddler', priceMin: 600, sort: 'price-asc' } });
    expect(el.querySelector<HTMLInputElement>('input[name="size"][value="7-8y"]')!.checked).toBe(true);
    expect(el.querySelector<HTMLInputElement>('input[name="size"][value="5-6y"]')!.checked).toBe(false);
    expect(el.querySelector<HTMLInputElement>('input[name="age"]')!.checked).toBe(true);
    expect(el.querySelector<HTMLInputElement>('input[name="priceMin"]')!.value).toBe('600');
    expect(el.querySelector<HTMLSelectElement>('select[name="sort"]')!.value).toBe('price-asc');
  });
});
