import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideEffects } from '@ngrx/effects';
import { Store, provideStore } from '@ngrx/store';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { SubscribeResult } from '../../models/newsletter.model';
import { NewsletterApi } from '../../services/newsletter-api.service';
import { NewsletterEffects } from '../../store/newsletter.effects';
import { newsletterFeature, selectNewsletterFooter } from '../../store/newsletter.reducer';
import { NewsletterFormComponent } from './newsletter-form.component';

describe('NewsletterFormComponent', () => {
  const subscribe = vi.fn<(email: string) => SubscribeResult>();

  function render(): ComponentFixture<NewsletterFormComponent> {
    TestBed.configureTestingModule({
      providers: [
        provideStore({ [newsletterFeature.name]: newsletterFeature.reducer }),
        provideEffects(NewsletterEffects),
        { provide: NewsletterApi, useValue: { subscribe: (e: string) => of(subscribe(e)) } },
      ],
    });
    const f = TestBed.createComponent(NewsletterFormComponent);
    f.detectChanges();
    return f;
  }
  const el = (f: ComponentFixture<unknown>) => f.nativeElement as HTMLElement;
  const type = (f: ComponentFixture<unknown>, value: string) => {
    const input = el(f).querySelector('input')!;
    input.value = value;
    input.dispatchEvent(new Event('input'));
  };
  const submit = (f: ComponentFixture<unknown>) => {
    el(f).querySelector('form')!.dispatchEvent(new Event('submit', { cancelable: true }));
    f.detectChanges();
  };

  beforeEach(() => subscribe.mockReset().mockReturnValue('ok'));

  it('NLS-1 NFR-19 has a labelled email field, a button, a help line and a polite live region', () => {
    const f = render();
    const input = el(f).querySelector<HTMLInputElement>('input[type=email]')!;
    expect(el(f).querySelector(`label[for="${input.id}"]`)!.textContent).toContain('Email address');
    expect(input.autocomplete).toBe('email');
    expect(el(f).querySelector('button[type=submit]')!.textContent).toContain('Join');
    const status = el(f).querySelector('[role=status]')!;
    expect(status.getAttribute('aria-live')).toBe('polite');
    expect(input.getAttribute('aria-describedby')).toContain(status.id);
  });

  it('NLS-1 NLS-6 posting without JavaScript goes to the storefront server', () => {
    const form = el(render()).querySelector('form')!;
    expect(form.getAttribute('method')).toBe('post');
    expect(form.getAttribute('action')).toBe('/newsletter/subscribe');
  });

  it('NLS-1 submitting dispatches the typed address and announces the same thank-you for any address', () => {
    const f = render();
    type(f, '  sara@example.com ');
    submit(f);
    expect(subscribe).toHaveBeenCalledWith('sara@example.com');
    expect(el(f).querySelector('[role=status]')!.textContent).toContain('Thank you');
  });

  it('NLS-6 a refused address is announced and marked invalid; typing again clears it', () => {
    subscribe.mockReturnValue('invalid');
    const f = render();
    type(f, 'sara@example.com');
    submit(f);
    expect(el(f).querySelector('[role=status]')!.textContent).toContain('does not look like an email');
    expect(el(f).querySelector('input')!.getAttribute('aria-invalid')).toBe('true');

    type(f, 'sara@example.co');
    f.detectChanges();
    expect(TestBed.inject(Store).selectSignal(selectNewsletterFooter)()).toBe('idle');
    expect(el(f).querySelector('input')!.getAttribute('aria-invalid')).toBeNull();
  });

  it('an empty field never reaches the server', () => {
    const f = render();
    submit(f);
    expect(subscribe).not.toHaveBeenCalled();
  });
});
