import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { NotFoundComponent, NotFoundKind } from './not-found.component';

const links = [{ label: 'New in', href: '/new-in' }, { label: 'Girls', href: '/girls' }];

function render(kind: NotFoundKind): HTMLElement {
  TestBed.configureTestingModule({ providers: [provideRouter([])] });
  const fixture = TestBed.createComponent(NotFoundComponent);
  fixture.componentRef.setInput('kind', kind);
  fixture.componentRef.setInput('links', links);
  fixture.detectChanges();
  return fixture.nativeElement as HTMLElement;
}

describe('NotFoundComponent', () => {
  it('PR-10 an unknown address shows a decorative 404, one h1, a home link and the top-level links', () => {
    const el = render('page-missing');

    expect(el.querySelector('.rp-notfound__code')?.getAttribute('aria-hidden')).toBe('true');
    expect(el.querySelector('.rp-notfound__code')?.textContent?.trim()).toBe('404');
    expect(el.querySelectorAll('h1')).toHaveLength(1);
    expect(el.querySelector('h1')?.textContent).toContain('We could not find that page');
    expect(el.querySelector('a[href="/"]')?.textContent).toContain('Back to home');
    expect([...el.querySelectorAll('a.rp-link')].map((a) => a.getAttribute('href'))).toEqual(['/new-in', '/girls']);
  });

  it('PR-11 a removed product says it has left the collection and offers a way on, without a 404 code', () => {
    const el = render('product-gone');

    expect(el.querySelector('h1')?.textContent).toContain('This piece has left the collection');
    expect(el.querySelector('.rp-notfound__code')).toBeNull();
    expect(el.querySelector('a[href="/new-in"]')).not.toBeNull();
  });

  it('a removed page says so', () => {
    expect(render('page-gone').querySelector('h1')?.textContent).toContain('This page has been removed');
  });

  it('PR-12 a temporary failure asks the visitor to try again', () => {
    const el = render('error');

    expect(el.querySelector('h1')?.textContent).toContain('Something went wrong');
    expect(el.textContent).toContain('Please try again in a moment.');
    expect(el.querySelector('.rp-notfound__code')).toBeNull();
  });
});
