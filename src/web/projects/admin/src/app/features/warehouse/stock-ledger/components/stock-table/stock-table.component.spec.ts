import { TestBed } from '@angular/core/testing';
import { stockRow } from '@app/testing/warehouse-fixtures';
import { textOf } from '@app/testing/production-fixtures';
import { StockVariantRowDto } from '../../models';
import { StockTableComponent } from './stock-table.component';

describe('StockTableComponent (spec LED-5, LED-6)', () => {
  function create(rows: StockVariantRowDto[], loading = false) {
    const fixture = TestBed.createComponent(StockTableComponent);
    fixture.componentRef.setInput('rows', rows);
    fixture.componentRef.setInput('loading', loading);
    const history = vi.fn();
    fixture.componentInstance.history.subscribe(history);
    fixture.detectChanges();
    return { fixture, root: fixture.nativeElement as HTMLElement, history };
  }

  it('says when there is no stock, and shows loading', () => {
    expect(textOf(create([]).fixture)).toContain('No stock found.');
    expect(textOf(create([], true).fixture)).toContain('Loading…');
  });

  it('shows on hand split by grade, available, and every bin with its quantity (LED-5, LED-6)', () => {
    const text = textOf(create([stockRow()]).fixture);

    expect(text).toContain('ROMP-STY-DRESS-RED-2-3Y');
    expect(text).toContain('10 accepted, 3 concession');
    expect(text).toContain('A-03-2: 10');
    expect(text).toContain('B-01-1: 3');
    expect(text).toContain('(concession)');
  });

  it('flags a retired barcode, and says when a variant is in no bin', () => {
    const text = textOf(create([stockRow({ gtinRetired: true, bins: [] })]).fixture);

    expect(text).toContain('Barcode retired');
    expect(text).toContain('In no bin');
  });

  it('is a real table with column headers and a caption, and the Movements button emits the row', () => {
    const { root, history } = create([stockRow()]);

    expect(root.querySelector('caption')?.textContent).toContain('Stock by variant');
    expect(Array.from(root.querySelectorAll('th')).every((th) => th.getAttribute('scope') === 'col')).toBe(true);
    (root.querySelector('button') as HTMLButtonElement).click();
    expect(history).toHaveBeenCalledWith(expect.objectContaining({ variantRefId: 31 }));
  });
});
