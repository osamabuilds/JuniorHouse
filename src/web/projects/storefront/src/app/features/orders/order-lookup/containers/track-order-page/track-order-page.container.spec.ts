import { RESPONSE_INIT } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Meta } from '@angular/platform-browser';
import { provideRouter } from '@angular/router';
import { provideStore } from '@ngrx/store';
import { provideEffects } from '@ngrx/effects';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { CustomerAuthApi } from '../../../../../core/customer-auth/services/customer-auth-api.service';
import { provideSiteSettings } from '../../../../storefront/site-settings';
import { LookupInput, LookupResult, TrackedOrder } from '../../models/order-lookup.model';
import { provideOrderLookup } from '../../order-lookup.providers';
import { OrderLookupApi } from '../../services/order-lookup-api.service';
import { TrackOrderPageContainer } from './track-order-page.container';

const order: TrackedOrder = {
  orderNumber: 'RMP-2026-00001-4', statusCode: 'PNDC', statusName: 'Waiting for your confirmation', placedAt: '2026-11-05T10:00:00Z', paymentMethodCode: 'COD',
  maskedMobile: '03XX-XXX4567', city: 'Lahore', subtotal: 2900, discount: 0, shipping: 250, total: 3150, lines: [{ name: 'Floral Dress', colour: 'Red', size: '2-3Y', quantity: 2, lineTotal: 2400 }],
};

function render(result: LookupResult = { kind: 'found', order }, responseInit?: ResponseInit) {
  const lookup = vi.fn((_i: LookupInput) => of(result));
  TestBed.configureTestingModule({
    providers: [
      provideRouter([]),
      provideStore({}),
      provideEffects(),
      provideSiteSettings(),
      provideOrderLookup(),
      { provide: OrderLookupApi, useValue: { lookup, byLink: () => of(result) } },
      { provide: CustomerAuthApi, useValue: { config: () => of({ turnstileSiteKey: '' }) } },
      ...(responseInit ? [{ provide: RESPONSE_INIT, useValue: responseInit }] : []),
    ],
  });
  const fixture = TestBed.createComponent(TrackOrderPageContainer);
  fixture.detectChanges();
  TestBed.flushEffects?.();
  fixture.detectChanges();
  const el = fixture.nativeElement as HTMLElement;
  const fill = (name: string, value: string) => {
    const input = el.querySelector<HTMLInputElement>(`input[name="${name}"]`)!;
    input.value = value;
    input.dispatchEvent(new Event('input'));
  };
  const submit = () => {
    el.querySelector('form')!.dispatchEvent(new Event('submit'));
    fixture.detectChanges();
  };
  return { fixture, el, lookup, fill, submit };
}

describe('TrackOrderPageContainer', () => {
  it('OLK-9 has one h1, labelled inputs and a labelled form', () => {
    const { el } = render();

    expect(el.querySelectorAll('h1').length).toBe(1);
    expect(el.querySelector('form')?.getAttribute('aria-label')).toBe('Track your order');
    for (const input of Array.from(el.querySelectorAll('input'))) {
      expect(el.querySelector(`label[for="${input.id}"]`), input.name).not.toBeNull();
    }
  });

  it('OLK-9 the page is noindex and its response is never cached', () => {
    const init: ResponseInit = {};
    const { fixture } = render(undefined, init);
    fixture.detectChanges();

    expect(TestBed.inject(Meta).getTag('name="robots"')!.content).toBe('noindex, nofollow');
    expect(new Headers(init.headers).get('Cache-Control')).toBe('private, no-store');
  });

  it('blank fields are named in words and nothing is sent', () => {
    const { el, lookup, submit } = render();

    submit();

    expect(el.textContent).toContain('Enter your order number.');
    expect(el.textContent).toContain('Enter the mobile number you gave at checkout.');
    expect(lookup).not.toHaveBeenCalled();
  });

  it('OLK-1 a match shows the status, the items and the totals, with the masked mobile', () => {
    const { el, fill, submit, lookup } = render();
    fill('orderNumber', ' RMP-2026-00001-4 ');
    fill('mobile', '0300 1234567');

    submit();

    expect(lookup).toHaveBeenCalledWith({ orderNumber: 'RMP-2026-00001-4', mobile: '0300 1234567', captchaToken: null });
    expect(el.querySelector('h2')?.textContent).toContain('Waiting for your confirmation');
    expect(el.textContent).toContain('Floral Dress');
    expect(el.textContent).toContain('03XX-XXX4567');
    expect(el.querySelectorAll('h1').length).toBe(1);
  });

  it('OLK-2 a mismatch says the same thing whichever field was wrong, and does not name either', () => {
    const { el, fill, submit } = render({ kind: 'mismatch' });
    fill('orderNumber', 'RMP-2026-00001-4');
    fill('mobile', '03009999999');

    submit();

    const alert = el.querySelector('[role="alert"]')!;
    expect(alert.textContent).toContain('Those details do not match an order');
    expect(alert.textContent).not.toMatch(/order number is|mobile is|wrong/i);
  });

  it('OLK-3 a rate limit asks the visitor to wait', () => {
    const { el, fill, submit } = render({ kind: 'limited' });
    fill('orderNumber', 'RMP-2026-00001-4');
    fill('mobile', '03009999999');

    submit();

    expect(el.querySelector('[role="alert"]')?.textContent).toContain('Too many attempts');
  });

  it('OLK-4 once the server asks for the check, the next attempt carries a token', () => {
    const { fixture, el, fill, submit, lookup } = render({ kind: 'captcha' });
    fill('orderNumber', 'RMP-2026-00001-4');
    fill('mobile', '03009999999');
    submit();
    fixture.detectChanges();

    expect(el.querySelector('rp-turnstile')).not.toBeNull();
    expect(el.querySelector('[role="alert"]')?.textContent).toContain('One more step');
    lookup.mockClear();
    submit();

    expect(lookup).toHaveBeenCalledTimes(1);
    expect(lookup.mock.calls[0][0].captchaToken).not.toBeNull();
  });

  it('looking up another order brings the form back', () => {
    const { el, fixture, fill, submit } = render();
    fill('orderNumber', 'RMP-2026-00001-4');
    fill('mobile', '03001234567');
    submit();

    (Array.from(el.querySelectorAll('button')).find((b) => b.textContent?.includes('Look up another order')) as HTMLButtonElement).click();
    fixture.detectChanges();

    expect(el.querySelector('form')).not.toBeNull();
    expect(el.querySelector('rp-tracked-order')).toBeNull();
  });
});
