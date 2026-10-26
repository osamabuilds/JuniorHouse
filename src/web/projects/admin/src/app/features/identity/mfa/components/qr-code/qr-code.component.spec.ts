import { TestBed } from '@angular/core/testing';
import { QrCodeComponent } from './qr-code.component';

describe('QrCodeComponent (mfa AC-2, plan: the QR is drawn in the browser)', () => {
  function create(text: string | null, label = 'QR code for your authenticator app') {
    const fixture = TestBed.createComponent(QrCodeComponent);
    fixture.componentRef.setInput('text', text);
    fixture.componentRef.setInput('label', label);
    fixture.detectChanges();
    return { fixture, root: fixture.nativeElement as HTMLElement };
  }

  it('draws one square SVG with a text alternative', () => {
    const { root } = create('otpauth://totp/Romp:amna?secret=ABC');

    const svg = root.querySelector('svg');
    expect(svg).not.toBeNull();
    expect(svg?.getAttribute('role')).toBe('img');
    expect(svg?.getAttribute('aria-label')).toBe('QR code for your authenticator app');
    const [, , width, height] = (svg?.getAttribute('viewBox') ?? '').split(' ').map(Number);
    expect(width).toBe(height);
    expect(root.querySelectorAll('path').length).toBeGreaterThan(0);
  });

  it('has the quiet zone a scanner needs around the code', () => {
    const { root } = create('hello');

    // Version 1 is 21 modules wide; four modules of quiet zone on each side makes 29.
    expect(root.querySelector('svg')?.getAttribute('viewBox')).toBe('0 0 29 29');
  });

  it('draws a different pattern for different text', () => {
    const a = create('otpauth://totp/Romp:amna?secret=AAAA').root.querySelector('path')?.getAttribute('d');
    const b = create('otpauth://totp/Romp:amna?secret=BBBB').root.querySelector('path')?.getAttribute('d');

    expect(a).toBeTruthy();
    expect(a).not.toBe(b);
  });

  it('draws nothing without text', () => {
    const { root } = create(null);

    expect(root.querySelector('svg')).toBeNull();
  });

  it('never loads anything from the network: no image, no external address (the secret stays in the browser)', () => {
    const { root } = create('otpauth://totp/Romp:amna?secret=ABC');

    expect(root.querySelector('img, image, use, link, script, iframe')).toBeNull();
    expect(root.innerHTML).not.toMatch(/https?:/);
  });

  it('has a dark pattern on a light background, so it scans in dark mode too', () => {
    const { root } = create('hello');

    expect(root.querySelector('rect')?.getAttribute('fill')).toBe('#ffffff');
    expect(root.querySelector('path')?.getAttribute('fill')).toBe('#000000');
  });
});
