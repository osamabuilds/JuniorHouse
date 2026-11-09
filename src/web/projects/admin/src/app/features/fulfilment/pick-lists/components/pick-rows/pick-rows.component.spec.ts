import { TestBed } from '@angular/core/testing';
import { PickRow } from '../../models';
import { PickRowsComponent } from './pick-rows.component';

const rows: PickRow[] = [
  { lineId: 1, variantId: 9, binCode: 'A-02-3', sku: 'SKU-9', name: 'Floral Dress', colour: 'Red', size: '2-3Y', quantity: 2, picked: 1, status: 'ALLOCATED' },
  { lineId: 2, variantId: 10, binCode: 'C-10-1', sku: 'SKU-10', name: 'Denim Jacket', colour: 'Blue', size: '4-5Y', quantity: 1, picked: 1, status: 'PICKED' },
  { lineId: 3, variantId: 11, binCode: '', sku: 'SKU-11', name: 'Rain Coat', colour: 'Yellow', size: '3-4Y', quantity: 1, picked: 0, status: 'AWAITING_RESTOCK' },
];

describe('PickRowsComponent (spec PCK-1)', () => {
  function create(currentBin: string | null = null) {
    const fixture = TestBed.createComponent(PickRowsComponent);
    fixture.componentRef.setInput('rows', rows);
    fixture.componentRef.setInput('currentBin', currentBin);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  it('PCK-1 shows shelf, item, size, colour and how many are picked, in the order given', () => {
    const root = create();
    const first = root.querySelectorAll('tbody tr')[0];

    expect(first.textContent).toContain('A-02-3');
    expect(first.textContent).toContain('Floral Dress');
    expect(first.textContent).toContain('2-3Y');
    expect(first.textContent).toContain('Red');
    expect(first.textContent).toContain('1 of 2');
    expect(first.textContent).toContain('To pick');
  });

  it('a finished row says Done, and a line waiting for restock has no shelf and says so in words', () => {
    const root = create();
    const bodyRows = root.querySelectorAll('tbody tr');

    expect(bodyRows[1].textContent).toContain('Done');
    expect(bodyRows[2].textContent).toContain('No shelf');
    expect(bodyRows[2].textContent).toContain('Waiting for restock');
  });

  it('the shelf the picker stands at is marked', () => {
    const root = create('C-10-1');

    expect(root.querySelectorAll('tbody tr')[1].classList).toContain('is-current');
    expect(root.querySelectorAll('tbody tr')[0].classList).not.toContain('is-current');
  });
});
