import { ComponentFixture, TestBed } from '@angular/core/testing';
import { NotifyDemandRow } from '../../models';
import { NotifyDemandTableComponent } from './notify-demand-table.component';

const rows: readonly NotifyDemandRow[] = [
  { variantId: 11, productName: 'Floral dress', colourName: 'Navy', sizeName: '4-5 years', skuCode: 'SKU-11', waiting: 12, oldestRequestAt: '2026-11-01T09:00:00Z' },
  { variantId: 12, productName: 'Old product', colourName: '', sizeName: '', skuCode: null, waiting: 2, oldestRequestAt: '2026-10-20T09:00:00Z' },
];

function render(): ComponentFixture<NotifyDemandTableComponent> {
  const f = TestBed.createComponent(NotifyDemandTableComponent);
  f.componentRef.setInput('rows', rows);
  f.detectChanges();
  return f;
}

describe('NotifyDemandTableComponent', () => {
  it('NTM-11 is a table with a caption, column headers and a row header per product', () => {
    const el = render().nativeElement as HTMLElement;

    expect(el.querySelector('caption')!.textContent).toContain('most waiting first');
    expect([...el.querySelectorAll('thead th')].every((th) => th.getAttribute('scope') === 'col')).toBe(true);
    expect([...el.querySelectorAll('tbody th')].map((th) => th.getAttribute('scope'))).toEqual(['row', 'row']);
  });

  it('NTM-11 shows product, colour, size, SKU, the number waiting and the oldest request', () => {
    const cells = [...(render().nativeElement as HTMLElement).querySelectorAll('tbody tr')[0].children].map((c) => c.textContent!.trim());

    expect(cells).toEqual(['Floral dress', 'Navy', '4-5 years', 'SKU-11', '12', '1 Nov 2026']);
  });

  it('NTM-11 a product that is no longer published still shows its count, with dashes for what is unknown', () => {
    const cells = [...(render().nativeElement as HTMLElement).querySelectorAll('tbody tr')[1].children].map((c) => c.textContent!.trim());

    expect(cells.slice(1, 5)).toEqual(['–', '–', '–', '2']);
  });

  it('NTM-12 nothing that looks like an email address is rendered', () => {
    expect((render().nativeElement as HTMLElement).textContent).not.toContain('@');
  });
});
