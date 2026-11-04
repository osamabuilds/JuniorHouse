import { TestBed } from '@angular/core/testing';
import { PromoCodeComponent } from './promo-code.component';

function render(inputs: { appliedCode?: string | null; error?: string | null; busy?: boolean } = {}) {
  TestBed.resetTestingModule();
  const fixture = TestBed.createComponent(PromoCodeComponent);
  for (const [name, value] of Object.entries(inputs)) fixture.componentRef.setInput(name, value);
  fixture.detectChanges();
  const el = fixture.nativeElement as HTMLElement;
  const emitted: { apply: string[]; remove: number } = { apply: [], remove: 0 };
  fixture.componentInstance.apply.subscribe((c) => emitted.apply.push(c));
  fixture.componentInstance.remove.subscribe(() => emitted.remove++);
  return { fixture, el, emitted };
}

function type(fixture: ReturnType<typeof render>['fixture'], value: string): void {
  const input = (fixture.nativeElement as HTMLElement).querySelector('input') as HTMLInputElement;
  input.value = value;
  input.dispatchEvent(new Event('input'));
  fixture.detectChanges();
}

describe('PromoCodeComponent', () => {
  it('FR-25 has a labelled box and does nothing until the shopper presses Apply', () => {
    const { el, emitted } = render();

    expect(el.querySelector('label')?.textContent).toContain('Promo code');
    expect(emitted.apply).toEqual([]);
  });

  it('FR-25 Apply sends the typed code, trimmed', () => {
    const { fixture, el, emitted } = render();
    type(fixture, '  welcome10 ');

    (el.querySelector('form') as HTMLFormElement).dispatchEvent(new Event('submit'));

    expect(emitted.apply).toEqual(['welcome10']);
  });

  it('keeps Apply off while the box is empty or the server is busy', () => {
    const empty = render();
    expect((empty.el.querySelector('button[type=submit]') as HTMLButtonElement).disabled).toBe(true);

    const busy = render({ busy: true });
    type(busy.fixture, 'abc');
    expect((busy.el.querySelector('button[type=submit]') as HTMLButtonElement).disabled).toBe(true);
  });

  it('FR-26 shows the server\'s reason under the box in words', () => {
    const { el } = render({ error: 'That code has expired.' });

    expect(el.textContent).toContain('That code has expired.');
    expect(el.querySelector('input')?.getAttribute('aria-invalid')).toBe('true');
  });

  it('DSC-4 an applied code replaces the box with a chip that can be removed', () => {
    const { el, emitted } = render({ appliedCode: 'WELCOME10' });

    expect(el.querySelector('input')).toBeNull();
    expect(el.querySelector('.rp-promo-chip')?.textContent).toContain('WELCOME10');
    (el.querySelector('.rp-promo-chip button') as HTMLButtonElement).click();
    expect(emitted.remove).toBe(1);
    expect(el.querySelector('.rp-promo-chip button')?.getAttribute('aria-label')).toBe('Remove code WELCOME10');
  });
});
