import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { chart } from '@app/testing/catalog-fixtures';
import { textOf } from '@app/testing/production-fixtures';
import { SizeChartViewComponent, rangeText } from './size-chart-view.component';

describe('rangeText', () => {
  it('writes a range, one end, or a dash', () => {
    expect(rangeText(12, 24)).toBe('12 to 24');
    expect(rangeText(12, null)).toBe('12');
    expect(rangeText(null, null)).toBe('–');
  });
});

describe('SizeChartViewComponent (spec CHT-1, CHT-3, CHT-6, CHT-7)', () => {
  function create(value = chart()) {
    TestBed.configureTestingModule({ providers: [provideRouter([])] });
    const fixture = TestBed.createComponent(SizeChartViewComponent);
    fixture.componentRef.setInput('chart', value);
    fixture.detectChanges();
    return { fixture, root: fixture.nativeElement as HTMLElement };
  }

  it('is a real table with header cells and a row header per size, never an image (CHT-6)', () => {
    const { root } = create();

    expect(root.querySelectorAll('thead th[scope="col"]').length).toBe(7);
    expect(root.querySelector('tbody th[scope="row"]')?.textContent).toContain('1-2 Years');
    expect(root.querySelector('img')).toBeNull();
    expect(root.textContent).toContain('12 to 24');
  });

  it('says a missing chart does not block publishing (CHT-3)', () => {
    const { fixture } = create(chart({ found: false, chartId: null, name: null, lines: [] }));

    expect(textOf(fixture)).toContain('can still be published');
  });
});
