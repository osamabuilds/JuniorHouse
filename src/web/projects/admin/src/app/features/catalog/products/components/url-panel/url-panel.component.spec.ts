import { TestBed } from '@angular/core/testing';
import { lookup, product } from '@app/testing/catalog-fixtures';
import { textOf } from '@app/testing/production-fixtures';
import { SetPathValue } from '../../models';
import { UrlPanelComponent } from './url-panel.component';

describe('UrlPanelComponent (spec URL-1..3, URL-7)', () => {
  function create(status: 'Draft' | 'Published' = 'Draft') {
    const fixture = TestBed.createComponent(UrlPanelComponent);
    fixture.componentRef.setInput('product', product({ status }));
    fixture.componentRef.setInput('genders', [lookup(1, 'Boys'), lookup(2, 'Girls')]);
    fixture.componentRef.setInput('categories', [lookup(3, 'Dresses')]);
    fixture.componentRef.setInput('history', [
      { path: '/girls/dresses/floral-summer-dress', isCurrent: true, firstUsedAt: '2026-10-01T00:00:00Z' },
      { path: '/girls/dresses/old-name', isCurrent: false, firstUsedAt: '2026-09-01T00:00:00Z' },
    ]);
    const saved = vi.fn<(value: SetPathValue) => void>();
    fixture.componentInstance.saved.subscribe(saved);
    fixture.detectChanges();
    return { fixture, root: fixture.nativeElement as HTMLElement, saved };
  }

  it('shows the current address and previews the new one as the slug is typed (URL-1)', () => {
    const { fixture, root } = create();
    const slug = root.querySelector('#url-slug') as HTMLInputElement;
    slug.value = 'summer-dress';
    slug.dispatchEvent(new Event('input'));
    fixture.detectChanges();

    expect(textOf(fixture)).toContain('/girls/dresses/floral-summer-dress');
    expect(textOf(fixture)).toContain('/girls/dresses/summer-dress');
  });

  it('warns that a published address keeps working through a redirect (URL-2)', () => {
    const { fixture } = create('Published');

    expect(textOf(fixture)).toContain('old address still works');
  });

  it('lists every address the product has had, with the current one marked (URL-3)', () => {
    const { root } = create();

    const items = root.querySelectorAll('section li');
    expect(items.length).toBe(2);
    expect(items[0].textContent).toContain('Current');
    expect(items[1].textContent).toContain('Redirects to the current address');
  });

  it('labels its controls and saves the chosen parts (NFR-19)', () => {
    const { root, saved } = create();
    for (const id of ['url-gender', 'url-category', 'url-slug']) {
      expect(root.querySelector(`label[for="${id}"]`)).not.toBeNull();
    }

    (root.querySelector('form') as HTMLFormElement).dispatchEvent(new Event('submit'));

    expect(saved).toHaveBeenCalledWith({ genderId: 2, categoryId: 3, slug: 'floral-summer-dress' });
  });
});
