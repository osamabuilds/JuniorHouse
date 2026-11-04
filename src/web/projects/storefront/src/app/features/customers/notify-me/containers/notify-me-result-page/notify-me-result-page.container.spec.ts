import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { provideStore } from '@ngrx/store';
import { BehaviorSubject } from 'rxjs';
import { SeoService } from '../../../../../shared/seo/seo.service';
import { provideSiteSettings } from '../../../../storefront/site-settings';
import { NotifyMeResultPageContainer } from './notify-me-result-page.container';

function render(status: string | null) {
  const seo = { set: vi.fn() };
  TestBed.configureTestingModule({
    providers: [
      provideRouter([]),
      provideStore({}),
      provideSiteSettings(),
      { provide: SeoService, useValue: seo },
      { provide: ActivatedRoute, useValue: { snapshot: { data: {} }, queryParamMap: new BehaviorSubject(convertToParamMap(status ? { status } : {})) } },
    ],
  });
  const f = TestBed.createComponent(NotifyMeResultPageContainer);
  f.detectChanges();
  return { el: f.nativeElement as HTMLElement, seo };
}

describe('NotifyMeResultPageContainer', () => {
  it('NTM-10 says the request was saved, with one h1 and a status region', () => {
    const { el } = render('ok');
    expect(el.querySelectorAll('h1').length).toBe(1);
    expect(el.querySelector('h1')!.textContent).toContain('We will let you know');
    expect(el.querySelector('[role=status]')).toBeTruthy();
  });

  it('NTM-6 an invalid request says so as an alert and never claims success', () => {
    const { el } = render('invalid');
    expect(el.querySelector('h1')!.textContent).toContain('could not save');
    expect(el.querySelector('[role=alert]')).toBeTruthy();
  });

  it.each([null, 'whatever'])('a missing or unknown status (%s) is an error, not success', (status) => {
    expect(render(status).el.querySelector('h1')!.textContent).toContain('could not save');
  });

  it('SEO-05 the page is noindex', () => {
    const { seo } = render('ok');
    expect(seo.set).toHaveBeenCalledWith(expect.objectContaining({ noindex: true, path: '/notify-me/requested' }));
  });
});
