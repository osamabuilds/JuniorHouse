import { TestBed } from '@angular/core/testing';
import { product, variant } from '@app/testing/catalog-fixtures';
import { textOf } from '@app/testing/production-fixtures';
import { VariantPriceInput } from '../../models';
import { VariantsTableComponent } from './variants-table.component';
import { provideAllowAllPermissions } from '@app/testing/provide-allow-all-permissions';

describe('VariantsTableComponent (spec PRD-1, PRD-2, PRD-8, PRD-13)', () => {
  beforeEach(() => TestBed.configureTestingModule({ providers: [provideAllowAllPermissions()] }));
  function create(variants = product().variants) {
    const fixture = TestBed.createComponent(VariantsTableComponent);
    fixture.componentRef.setInput('variants', variants);
    const saved = vi.fn<(prices: readonly VariantPriceInput[]) => void>();
    fixture.componentInstance.pricesSaved.subscribe(saved);
    fixture.detectChanges();
    const root = fixture.nativeElement as HTMLElement;
    const type = (label: string, value: string) => {
      const input = Array.from(root.querySelectorAll('label')).find((l) => l.textContent?.includes(label))!.querySelector('input') as HTMLInputElement;
      input.value = value;
      input.dispatchEvent(new Event('input'));
    };
    const save = () => {
      (Array.from(root.querySelectorAll('button')).find((b) => b.textContent?.includes('Save prices')) as HTMLButtonElement).click();
      fixture.detectChanges();
    };
    return { fixture, root, saved, type, save };
  }

  it('is a real table with header cells, a row header per colour, and every input labelled (NFR-19)', () => {
    const { root } = create();

    expect(root.querySelectorAll('thead th[scope="col"]').length).toBeGreaterThanOrEqual(5);
    expect(root.querySelectorAll('tbody th[scope="row"]').length).toBe(2);
    expect(root.textContent).toContain('Price for Red 1-2 Years');
    expect(root.textContent).toContain('Compare-at price for Red 2-3 Years');
  });

  it('shows the SKU and barcode once Labelling has issued them, and "Not yet available" until then (PRD-1)', () => {
    const { fixture } = create();

    const text = textOf(fixture);
    expect(text).toContain('ROMP-STY-DRESS-RED-1-2Y');
    expect(text).toContain('0200000000019');
    expect(text).toContain('Not yet available');
  });

  it('says why a variant cannot be bought yet (PRD-13, PRD-14)', () => {
    const { fixture } = create([
      variant({ id: 1, availability: 'Unpriced', isSellable: false, price: null }),
      variant({ id: 2, colourId: 2, colourName: 'Blue', availability: 'GtinVoided', isSellable: false }),
    ]);

    const text = textOf(fixture);
    expect(text).toContain('Needs a price');
    expect(text).toContain('Barcode voided');
  });

  it('sends only the prices that changed, as numbers (PRD-8)', () => {
    const { type, save, saved } = create();
    type('Price for Red 1-2 Years', '1500');
    type('Compare-at price for Red 1-2 Years', '1800');

    save();

    expect(saved).toHaveBeenCalledWith([{ colourId: 1, sizeId: 4, price: 1500, compareAtPrice: 1800 }]);
  });

  it('refuses a compare-at price that is not higher than the price, before sending (PRD-2, D4)', () => {
    const { type, save, saved, fixture } = create();
    type('Price for Red 1-2 Years', '1000');
    type('Compare-at price for Red 1-2 Years', '900');

    save();

    expect(saved).not.toHaveBeenCalled();
    expect(textOf(fixture)).toContain('compare-at price must be higher');
  });

  it('refuses a zero, negative or non-numeric price (PRD-8)', () => {
    for (const bad of ['0', '-5', 'abc']) {
      const { type, save, saved, fixture } = create();
      type('Price for Red 1-2 Years', bad);

      save();

      expect(saved).not.toHaveBeenCalled();
      expect(textOf(fixture)).toContain('must be more than 0');
    }
  });

  it('is read-only when the product is archived', () => {
    const fixture = TestBed.createComponent(VariantsTableComponent);
    fixture.componentRef.setInput('variants', product().variants);
    fixture.componentRef.setInput('disabled', true);
    fixture.detectChanges();

    const inputs = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('input'));
    expect(inputs.every((input) => input.disabled)).toBe(true);
  });
});
