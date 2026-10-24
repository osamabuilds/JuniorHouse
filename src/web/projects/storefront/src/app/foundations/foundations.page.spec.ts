import { TestBed } from '@angular/core/testing';
import { provideRpImageLoader } from '../shared/ui/image/image-loader';
import { FoundationsPage } from './foundations.page';

describe('FoundationsPage', () => {
  it('DSF-13 has one h1, landmarks and no skipped heading levels', () => {
    TestBed.configureTestingModule({ providers: [provideRpImageLoader()] });
    const f = TestBed.createComponent(FoundationsPage);
    f.detectChanges();
    const el = f.nativeElement as HTMLElement;
    expect(el.querySelectorAll('h1').length).toBe(1);
    // The app shell supplies the header, main and footer landmarks; the page itself must not repeat them.
    expect(el.querySelector('main')).toBeNull();
    expect(el.querySelector('header, footer')).toBeNull();
    const levels = Array.from(el.querySelectorAll('h1,h2,h3,h4')).map((h) => Number(h.tagName[1]));
    levels.reduce((prev, cur) => {
      expect(cur - prev).toBeLessThanOrEqual(1);
      return cur;
    }, 0);
  });

  it('DSF-13 gives every image an alt attribute and every control an accessible name', () => {
    TestBed.configureTestingModule({ providers: [provideRpImageLoader()] });
    const f = TestBed.createComponent(FoundationsPage);
    f.detectChanges();
    const el = f.nativeElement as HTMLElement;
    el.querySelectorAll('img').forEach((i) => expect(i.hasAttribute('alt')).toBe(true));
    el.querySelectorAll('button').forEach((b) =>
      expect((b.textContent ?? '').trim() || b.getAttribute('aria-label')).toBeTruthy(),
    );
    el.querySelectorAll('input').forEach((i) => expect(el.querySelector(`label[for="${i.id}"]`)).not.toBeNull());
  });
});
