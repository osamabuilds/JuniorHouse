import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { provideStore } from '@ngrx/store';
import { BehaviorSubject } from 'rxjs';
import { SeoService } from '../../../../../shared/seo/seo.service';
import { provideSiteSettings } from '../../../../storefront/site-settings';
import { NewsletterResultPageContainer } from './newsletter-result-page.container';

function render(kind: string, status: string | null) {
  const seo = { set: vi.fn() };
  TestBed.configureTestingModule({
    providers: [
      provideRouter([]),
      provideStore({}),
      provideSiteSettings(),
      { provide: SeoService, useValue: seo },
      { provide: ActivatedRoute, useValue: { snapshot: { data: { kind } }, queryParamMap: new BehaviorSubject(convertToParamMap(status ? { status } : {})) } },
    ],
  });
  const f = TestBed.createComponent(NewsletterResultPageContainer);
  f.detectChanges();
  return { el: f.nativeElement as HTMLElement, seo };
}

describe('NewsletterResultPageContainer', () => {
  it('NLS-1 says the visitor is subscribed, with one h1', () => {
    const { el } = render('subscribed', 'ok');
    expect(el.querySelectorAll('h1').length).toBe(1);
    expect(el.querySelector('h1')!.textContent).toContain('You are subscribed');
    expect(el.querySelector('[role=status]')).toBeTruthy();
  });

  it('NLS-3 says the visitor is unsubscribed', () => {
    expect(render('unsubscribed', 'ok').el.querySelector('h1')!.textContent).toContain('You are unsubscribed');
  });

  it('NLS-3 an invalid link says so as an alert and never claims success', () => {
    const { el } = render('unsubscribed', 'invalid');
    expect(el.querySelector('h1')!.textContent).toContain('not valid');
    expect(el.querySelector('[role=alert]')).toBeTruthy();
  });

  it('a missing status is treated as an error, not as success', () => {
    expect(render('subscribed', null).el.querySelector('h1')!.textContent).toContain('could not');
  });

  it('an unknown status is treated as an error, not as success', () => {
    expect(render('subscribed', 'whatever').el.querySelector('h1')!.textContent).toContain('could not');
  });

  it('SEO-05 the page is noindex', () => {
    const { seo } = render('unsubscribed', 'ok');
    expect(seo.set).toHaveBeenCalledWith(expect.objectContaining({ noindex: true, path: '/newsletter/unsubscribed' }));
  });
});
