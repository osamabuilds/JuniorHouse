import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideEffects } from '@ngrx/effects';
import { provideStore } from '@ngrx/store';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { SeoService } from '../../../../../shared/seo/seo.service';
import { provideSiteSettings } from '../../../../storefront/site-settings';
import { MineResult } from '../../models/newsletter.model';
import { NewsletterApi } from '../../services/newsletter-api.service';
import { NewsletterEffects } from '../../store/newsletter.effects';
import { newsletterFeature } from '../../store/newsletter.reducer';
import { PreferencesPageContainer } from './preferences-page.container';

describe('PreferencesPageContainer', () => {
  const mine = vi.fn<() => MineResult>();
  const setMine = vi.fn<(on: boolean) => MineResult>();
  const seo = { set: vi.fn() };

  function render() {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        provideStore({ [newsletterFeature.name]: newsletterFeature.reducer }),
        provideEffects(NewsletterEffects),
        provideSiteSettings(),
        { provide: SeoService, useValue: seo },
        { provide: NewsletterApi, useValue: { mine: () => of(mine()), setMine: (on: boolean) => of(setMine(on)) } },
      ],
    });
    const f = TestBed.createComponent(PreferencesPageContainer);
    f.detectChanges();
    return f;
  }
  const el = (f: { nativeElement: unknown }) => f.nativeElement as HTMLElement;

  beforeEach(() => {
    mine.mockReset().mockReturnValue({ status: 'ok', subscribed: true });
    setMine.mockReset().mockImplementation((on) => ({ status: 'ok', subscribed: on }));
    seo.set.mockReset();
  });

  it('NLS-2 shows one h1 and a labelled checkbox that reflects the saved choice', () => {
    const f = render();
    expect(el(f).querySelectorAll('h1').length).toBe(1);
    const box = el(f).querySelector<HTMLInputElement>('input[type=checkbox]')!;
    expect(box.checked).toBe(true);
    expect(box.closest('label')!.textContent).toContain('Send me news and offers by email');
    expect(el(f).textContent).toContain('never carry offers');
  });

  it('NLS-2 TC-ACC-17 turning it off is sent to the API and stays off', () => {
    const f = render();
    const box = el(f).querySelector<HTMLInputElement>('input[type=checkbox]')!;
    box.checked = false;
    box.dispatchEvent(new Event('change'));
    f.detectChanges();
    expect(setMine).toHaveBeenCalledWith(false);
    expect(el(f).querySelector<HTMLInputElement>('input[type=checkbox]')!.checked).toBe(false);
  });

  it('NLS-2 a failed save is announced in the live region', () => {
    setMine.mockReturnValue({ status: 'error' });
    const f = render();
    const box = el(f).querySelector<HTMLInputElement>('input[type=checkbox]')!;
    box.dispatchEvent(new Event('change'));
    f.detectChanges();
    const note = el(f).querySelector('#rp-pref-note')!;
    expect(note.getAttribute('aria-live')).toBe('polite');
    expect(note.textContent).toContain('could not save');
  });

  it('a failed load offers to try again', () => {
    mine.mockReturnValue({ status: 'error' });
    const f = render();
    expect(el(f).querySelector('[role=alert]')!.textContent).toContain('could not load');
    mine.mockReturnValue({ status: 'ok', subscribed: false });
    el(f).querySelector<HTMLButtonElement>('button')!.click();
    f.detectChanges();
    expect(el(f).querySelector<HTMLInputElement>('input[type=checkbox]')!.checked).toBe(false);
  });

  it('SEO-05 the page is noindex', () => {
    render();
    expect(seo.set).toHaveBeenCalledWith(expect.objectContaining({ noindex: true, path: '/account/preferences' }));
  });
});
