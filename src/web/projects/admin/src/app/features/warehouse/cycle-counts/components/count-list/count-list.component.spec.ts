import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { countRow } from '@app/testing/warehouse-fixtures';
import { textOf } from '@app/testing/production-fixtures';
import { CycleCountRowDto } from '../../models';
import { CountListComponent } from './count-list.component';

describe('CountListComponent (spec CC-11)', () => {
  beforeEach(() => TestBed.configureTestingModule({ providers: [provideRouter([])] }));

  function create(counts: CycleCountRowDto[], loading = false) {
    const fixture = TestBed.createComponent(CountListComponent);
    fixture.componentRef.setInput('counts', counts);
    fixture.componentRef.setInput('loading', loading);
    fixture.detectChanges();
    return { fixture, root: fixture.nativeElement as HTMLElement };
  }

  it('says when there are no counts, and shows loading', () => {
    expect(textOf(create([]).fixture)).toContain('No counts found.');
    expect(textOf(create([], true).fixture)).toContain('Loading…');
  });

  it('shows the number, the status in words, who started it and how big it is', () => {
    const text = textOf(create([countRow(), countRow({ id: 61, countNo: 'CC-2026-0002', statusCode: 'CLOSED' })]).fixture);

    expect(text).toContain('CC-2026-0001');
    expect(text).toContain('Open');
    expect(text).toContain('Closed');
    expect(text).toContain('Ayesha Khan');
  });

  it('links to each count: "Open the count" while it is open, "View" afterwards, with column headers and a caption', () => {
    const { root } = create([countRow(), countRow({ id: 61, statusCode: 'CANCELLED' })]);

    const links = Array.from(root.querySelectorAll('a')) as HTMLAnchorElement[];
    expect(links.map((a) => a.textContent?.trim())).toEqual(['Open the count', 'View']);
    expect(links[0].getAttribute('href')).toBe('/warehouse/cycle-counts/60');
    expect(root.querySelector('caption')?.textContent).toContain('Cycle counts');
    expect(Array.from(root.querySelectorAll('th')).every((th) => th.getAttribute('scope') === 'col')).toBe(true);
  });
});
