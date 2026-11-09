import { TestBed } from '@angular/core/testing';
import { ScanBannerComponent } from './scan-banner.component';

describe('ScanBannerComponent (spec APP-4, NFR-19)', () => {
  function create(accepted: boolean, message: string) {
    const fixture = TestBed.createComponent(ScanBannerComponent);
    fixture.componentRef.setInput('accepted', accepted);
    fixture.componentRef.setInput('message', message);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  it('APP-4 a success says Accepted in words, with a tick and the success colour', () => {
    const root = create(true, 'Floral Dress, 2-3Y: 1 of 2.');
    const banner = root.querySelector('.scan-banner') as HTMLElement;

    expect(banner.textContent).toContain('Accepted');
    expect(banner.textContent).toContain('1 of 2');
    expect(banner.textContent).toContain('✔');
    expect(banner.classList).toContain('scan-banner--ok');
  });

  it('APP-4 a refusal says Refused in words, with a cross and the danger colour: not colour alone', () => {
    const root = create(false, 'This item is not on the order. Put it back.');
    const banner = root.querySelector('.scan-banner') as HTMLElement;

    expect(banner.textContent).toContain('Refused');
    expect(banner.textContent).toContain('Put it back');
    expect(banner.textContent).toContain('✖');
    expect(banner.classList).toContain('scan-banner--refused');
  });

  it('NFR-19 the answer is announced politely to a screen reader and the icon is hidden from it', () => {
    const root = create(false, 'Wrong shelf.');
    const banner = root.querySelector('.scan-banner') as HTMLElement;

    expect(banner.getAttribute('role')).toBe('status');
    expect(banner.getAttribute('aria-live')).toBe('polite');
    expect(banner.querySelector('.scan-banner__icon')?.getAttribute('aria-hidden')).toBe('true');
  });
});
