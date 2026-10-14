import { TestBed } from '@angular/core/testing';
import { lookup } from '@app/testing/catalog-fixtures';
import { textOf } from '@app/testing/production-fixtures';
import { SizeChartDto, SizeChartFormValue } from '../../models';
import { SizeChartEditorComponent } from './size-chart-editor.component';

describe('SizeChartEditorComponent (spec CHT-1, CHT-4, CHT-5, CHT-7)', () => {
  const sizes = [lookup(4, '1-2 Years'), lookup(5, '2-3 Years')];

  function create(chart: SizeChartDto | null = null) {
    const fixture = TestBed.createComponent(SizeChartEditorComponent);
    fixture.componentRef.setInput('chart', chart);
    fixture.componentRef.setInput('categories', [lookup(3, 'Dresses')]);
    fixture.componentRef.setInput('genders', [lookup(2, 'Girls')]);
    fixture.componentRef.setInput('sizes', sizes);
    fixture.componentRef.setInput('defaultPerson', 'Ayesha Khan');
    const submitted = vi.fn<(value: SizeChartFormValue & { person: string }) => void>();
    fixture.componentInstance.submitted.subscribe(submitted);
    fixture.detectChanges();
    const root = fixture.nativeElement as HTMLElement;
    const type = (selector: string, value: string) => {
      const input = root.querySelector(selector) as HTMLInputElement;
      input.value = value;
      input.dispatchEvent(new Event('input'));
    };
    const tick = (index: number) => {
      const box = root.querySelectorAll('tbody input[type="checkbox"]')[index] as HTMLInputElement;
      box.checked = true;
      box.dispatchEvent(new Event('change'));
      fixture.detectChanges();
    };
    const cell = (row: number, column: number) => root.querySelectorAll('tbody tr')[row].querySelectorAll('td input')[column] as HTMLInputElement;
    const submit = () => {
      (root.querySelector('form') as HTMLFormElement).dispatchEvent(new Event('submit'));
      fixture.detectChanges();
    };
    return { fixture, root, submitted, type, tick, cell, submit };
  }

  function fillNew(h: ReturnType<typeof create>) {
    h.type('#sce-name', 'Girls dresses');
    h.fixture.componentInstance.categoryId.set(3);
    h.fixture.componentInstance.genderId.set(2);
    h.tick(0);
  }

  it('is a real table: header cells, a row header per size, and every cell input labelled (CHT-7)', () => {
    const { root } = create();

    expect(root.querySelectorAll('thead th[scope="col"]').length).toBe(10);
    expect(root.querySelectorAll('tbody th[scope="row"]').length).toBe(2);
    expect(root.textContent).toContain('Height from (cm) for 1-2 Years');
    for (const id of ['sce-name', 'sce-category', 'sce-gender', 'sce-person']) {
      expect(root.querySelector(`label[for="${id}"]`)).not.toBeNull();
    }
  });

  it('only the ticked sizes can be typed into', () => {
    const { cell, tick } = create();

    expect(cell(0, 0).disabled).toBe(true);
    tick(0);
    expect(cell(0, 0).disabled).toBe(false);
    expect(cell(1, 0).disabled).toBe(true);
  });

  it('sends the ticked rows as numbers, blanks as null (CHT-1)', () => {
    const h = create();
    fillNew(h);
    h.type('tbody tr:first-child td:nth-child(2) input', '12');
    h.type('tbody tr:first-child td:nth-child(3) input', '24');

    h.submit();

    expect(h.submitted).toHaveBeenCalledTimes(1);
    const value = h.submitted.mock.calls[0][0];
    expect(value).toMatchObject({ categoryId: 3, genderId: 2, name: 'Girls dresses', person: 'Ayesha Khan' });
    expect(value.lines).toEqual([
      { sizeId: 4, ageMinMonths: 12, ageMaxMonths: 24, heightMinCm: null, heightMaxCm: null, weightMinKg: null, weightMaxKg: null, chestCm: null, waistCm: null, lengthCm: null },
    ]);
  });

  it('refuses a minimum above its maximum, naming the size (CHT-4)', () => {
    const h = create();
    fillNew(h);
    h.type('tbody tr:first-child td:nth-child(4) input', '100');
    h.type('tbody tr:first-child td:nth-child(5) input', '90');

    h.submit();

    expect(h.submitted).not.toHaveBeenCalled();
    expect(textOf(h.fixture)).toContain('1-2 Years: the lowest height is above the highest.');
  });

  it('refuses a negative or non-numeric value (CHT-4)', () => {
    const h = create();
    fillNew(h);
    h.type('tbody tr:first-child td:nth-child(8) input', '-3');

    h.submit();

    expect(h.submitted).not.toHaveBeenCalled();
    expect(textOf(h.fixture)).toContain('must be a number, not negative');
  });

  it('refuses a row with nothing in it, and a chart with no size ticked (FR-08)', () => {
    const h = create();
    fillNew(h);
    h.submit();
    expect(textOf(h.fixture)).toContain('give an age range or at least one measurement');

    const none = create();
    none.type('#sce-name', 'x');
    none.fixture.componentInstance.categoryId.set(3);
    none.fixture.componentInstance.genderId.set(2);
    none.submit();
    expect(textOf(none.fixture)).toContain('Tick at least one size.');
  });

  it('fills the rows of an existing chart, and keeps its category and gender fixed (CHT-5)', () => {
    const chart: SizeChartDto = {
      id: 4, categoryId: 3, genderId: 2, name: 'Girls dresses', unit: 'cm', version: 55,
      lines: [{ sizeId: 5, sizeName: '2-3 Years', ageMinMonths: 24, ageMaxMonths: 36, heightMinCm: null, heightMaxCm: null, weightMinKg: null, weightMaxKg: null, chestCm: 54, waistCm: null, lengthCm: null }],
    };
    const { root, cell } = create(chart);

    expect((root.querySelector('#sce-name') as HTMLInputElement).value).toBe('Girls dresses');
    expect((root.querySelector('#sce-category') as HTMLSelectElement).disabled).toBe(true);
    expect(cell(1, 0).value).toBe('24');
    expect(cell(1, 6).value).toBe('54');
    expect(cell(0, 0).disabled).toBe(true);
  });
});
