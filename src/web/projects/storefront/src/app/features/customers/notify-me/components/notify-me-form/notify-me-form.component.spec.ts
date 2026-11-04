import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideEffects } from '@ngrx/effects';
import { provideStore } from '@ngrx/store';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { NotifyResult } from '../../models/notify-me.model';
import { NotifyMeApi } from '../../services/notify-me-api.service';
import { NotifyMeEffects } from '../../store/notify-me.effects';
import { notifyMeFeature } from '../../store/notify-me.reducer';
import { NotifyMeFormComponent } from './notify-me-form.component';

describe('NotifyMeFormComponent', () => {
  const request = vi.fn<(variantId: number, email: string) => NotifyResult>();

  function render(variantId = 42): ComponentFixture<NotifyMeFormComponent> {
    TestBed.configureTestingModule({
      providers: [
        provideStore({ [notifyMeFeature.name]: notifyMeFeature.reducer }),
        provideEffects(NotifyMeEffects),
        { provide: NotifyMeApi, useValue: { request: (v: number, e: string) => of(request(v, e)) } },
      ],
    });
    const f = TestBed.createComponent(NotifyMeFormComponent);
    f.componentRef.setInput('variantId', variantId);
    f.componentRef.setInput('sizeName', '4-5 years');
    f.detectChanges();
    return f;
  }
  const el = (f: ComponentFixture<unknown>) => f.nativeElement as HTMLElement;
  const type = (f: ComponentFixture<unknown>, value: string) => {
    const input = el(f).querySelector('input[type=email]') as HTMLInputElement;
    input.value = value;
    input.dispatchEvent(new Event('input'));
  };
  const submit = (f: ComponentFixture<unknown>) => {
    el(f).querySelector('form')!.dispatchEvent(new Event('submit', { cancelable: true }));
    f.detectChanges();
  };

  beforeEach(() => request.mockReset().mockReturnValue({ status: 'ok' }));

  it('NTM-10 NFR-19 has a labelled email field, a button, help text and a polite live region', () => {
    const f = render();
    const input = el(f).querySelector<HTMLInputElement>('input[type=email]')!;
    expect(el(f).querySelector(`label[for="${input.id}"]`)!.textContent).toContain('Email me when it is back');
    expect(input.autocomplete).toBe('email');
    expect(el(f).querySelector('button[type=submit]')!.textContent).toContain('Notify me');
    const status = el(f).querySelector('[role=status]')!;
    expect(status.getAttribute('aria-live')).toBe('polite');
    expect(input.getAttribute('aria-describedby')).toContain(status.id);
    expect(el(f).querySelector('h2')!.textContent).toContain('4-5 years');
  });

  it('NTM-10 posting without JavaScript goes to the storefront server with the size', () => {
    const f = render(42);
    const form = el(f).querySelector('form')!;
    expect(form.getAttribute('method')).toBe('post');
    expect(form.getAttribute('action')).toBe('/notify-me');
    expect(el(f).querySelector<HTMLInputElement>('input[name=variantId]')!.value).toBe('42');
  });

  it('NTM-2 NTM-3 submitting sends the typed address and announces the thank-you', () => {
    const f = render(42);
    type(f, '  sara@example.com ');
    submit(f);
    expect(request).toHaveBeenCalledWith(42, 'sara@example.com');
    expect(el(f).querySelector('[role=status]')!.textContent).toContain('We will email you once');
  });

  it('NTM-6 shows the specific reason the server gave and marks the field invalid', () => {
    request.mockReturnValue({ status: 'invalid', message: 'That does not look like an email address.' });
    const f = render();
    type(f, 'nope@x.co');
    submit(f);
    expect(el(f).querySelector('[role=status]')!.textContent).toContain('does not look like an email');
    expect(el(f).querySelector('input[type=email]')!.getAttribute('aria-invalid')).toBe('true');
  });

  it('NTM-7 tells a rate-limited visitor to wait', () => {
    request.mockReturnValue({ status: 'busy' });
    const f = render();
    type(f, 'a@b.co');
    submit(f);
    expect(el(f).querySelector('[role=status]')!.textContent).toContain('Too many tries');
  });

  it('NTM-10 typing again clears the old answer', () => {
    const f = render();
    type(f, 'a@b.co');
    submit(f);
    type(f, 'a@b.com');
    f.detectChanges();
    expect(el(f).querySelector('[role=status]')!.textContent!.trim()).toBe('');
  });

  it('NTM-1 an empty field is not sent', () => {
    const f = render();
    submit(f);
    expect(request).not.toHaveBeenCalled();
  });
});
