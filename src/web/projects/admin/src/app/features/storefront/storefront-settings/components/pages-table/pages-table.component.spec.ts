import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { pageRow } from '@app/testing/storefront-fixtures';
import { textContent } from '@app/testing/dom-helpers';
import { PageRow } from '../../models';
import { PagesTableComponent } from './pages-table.component';

describe('PagesTableComponent (spec ST-8)', () => {
  function create(pages: PageRow[], loading = false) {
    TestBed.configureTestingModule({ providers: [provideRouter([])] });
    const fixture = TestBed.createComponent(PagesTableComponent);
    fixture.componentRef.setInput('pages', pages);
    fixture.componentRef.setInput('loading', loading);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  it('is a real table: column headers with scope, and the page title as a link to its editor', () => {
    const root = create([pageRow({ id: 7, title: 'Delivery' }), pageRow({ id: 8, slug: 'terms', title: 'Terms', status: 'Published', blockCount: 0 })]);

    expect(Array.from(root.querySelectorAll('thead th')).every((th) => th.getAttribute('scope') === 'col')).toBe(true);
    expect(root.querySelector('tbody th[scope="row"] a')?.getAttribute('href')).toBe('/storefront/pages/7');
    expect(textContent(root)).toContain('/delivery');
  });

  it('says the status in words, not only colour', () => {
    const root = create([pageRow({ status: 'Archived' })]);

    expect(root.querySelector('.badge')?.textContent?.trim()).toBe('Archived');
  });

  it('says when there is nothing', () => {
    expect(textContent(create([]))).toContain('There are no pages here.');
  });

  it('says when it is loading', () => {
    expect(textContent(create([], true))).toContain('Loading the pages');
  });
});
