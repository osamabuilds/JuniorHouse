import { RESPONSE_INIT } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Meta } from '@angular/platform-browser';
import { provideRouter } from '@angular/router';
import { provideStore } from '@ngrx/store';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { polyfillDialog } from '../../../../../layout/dialog/dialog.testing';
import { provideSiteSettings } from '../../../../storefront/site-settings';
import { Address, ChangeResult, LoadResult } from '../../models/address.model';
import { AddressesApi } from '../../services/addresses-api.service';
import { provideAddresses } from '../../addresses.providers';
import { AddressesPageContainer } from './addresses-page.container';

const address = (id: number, isDefault = false): Address => ({
  id, recipientName: `Home ${id}`, phone: '+923001234567', provinceId: 1, provinceName: 'Punjab', cityId: 10, cityName: 'Lahore',
  lineOne: `House ${id}, Street 4`, lineTwo: null, postcode: null, isDefault,
});

function render(initial: LoadResult, responseInit?: ResponseInit) {
  polyfillDialog();
  const list = vi.fn(() => of(initial));
  const remove = vi.fn((_id: number): ChangeResult => ({ status: 'ok' }));
  const makeDefault = vi.fn((_id: number): ChangeResult => ({ status: 'ok' }));
  TestBed.configureTestingModule({
    providers: [
      provideRouter([]),
      provideStore({}),
      provideSiteSettings(),
      provideAddresses(),
      {
        provide: AddressesApi,
        useValue: {
          list,
          create: () => of({ status: 'ok' }),
          update: () => of({ status: 'ok' }),
          remove: (id: number) => of(remove(id)),
          makeDefault: (id: number) => of(makeDefault(id)),
          provinces: () => of([{ id: 1, code: 'PB', name: 'Punjab' }]),
          cities: () => of([{ id: 10, code: 'LHE', name: 'Lahore', provinceId: 1 }]),
        },
      },
      ...(responseInit ? [{ provide: RESPONSE_INIT, useValue: responseInit }] : []),
    ],
  });
  const fixture = TestBed.createComponent(AddressesPageContainer);
  fixture.detectChanges();
  TestBed.flushEffects?.();
  fixture.detectChanges();
  return { fixture, el: fixture.nativeElement as HTMLElement, list, remove, makeDefault };
}

const ok = (items: Address[]): LoadResult => ({ status: 'ok', page: { items, page: 1, pageSize: 10, totalCount: items.length, totalPages: 1 } });

describe('AddressesPageContainer', () => {
  it('ADD-8 has one h1, lists each address under its own heading and marks the default with the word Default', () => {
    const { el } = render(ok([address(1, true), address(2)]));

    expect(el.querySelectorAll('h1').length).toBe(1);
    const items = el.querySelectorAll('ul.rp-address-list > li');
    expect(items.length).toBe(2);
    expect(items[0].querySelector('h2')!.textContent).toContain('Home 1');
    expect(items[0].textContent).toContain('Default');
    expect(items[1].textContent).not.toContain('Default');
  });

  it('ADD-2 offers Make default only on addresses that are not the default', () => {
    const { el } = render(ok([address(1, true), address(2)]));
    const items = el.querySelectorAll('ul.rp-address-list > li');

    expect(items[0].textContent).not.toContain('Make default');
    expect(items[1].textContent).toContain('Make default');
  });

  it('ADD-2 Make default sends that address id', () => {
    const { el, makeDefault } = render(ok([address(1, true), address(2)]));

    el.querySelector<HTMLButtonElement>('ul.rp-address-list > li:nth-child(2) button[data-make-default]')!.click();

    expect(makeDefault).toHaveBeenCalledWith(2);
  });

  it('ADD-8 Remove asks first, in a dialog, and only the confirm button removes', () => {
    const { el, fixture, remove } = render(ok([address(1, true)]));

    el.querySelector<HTMLButtonElement>('button[data-remove]')!.click();
    fixture.detectChanges();
    const dialog = el.querySelector<HTMLDialogElement>('dialog[data-confirm]')!;
    expect(dialog.hasAttribute('open')).toBe(true);
    expect(dialog.textContent).toContain('Home 1');
    expect(remove).not.toHaveBeenCalled();

    dialog.querySelector<HTMLButtonElement>('button[data-confirm-remove]')!.click();
    expect(remove).toHaveBeenCalledWith(1);
  });

  it('ADD-1 Add opens the form dialog with an empty form', () => {
    const { el, fixture } = render(ok([]));

    el.querySelector<HTMLButtonElement>('button[data-add]')!.click();
    fixture.detectChanges();

    const dialog = el.querySelector<HTMLDialogElement>('dialog[data-form]')!;
    expect(dialog.hasAttribute('open')).toBe(true);
    expect(dialog.querySelector('h2')!.textContent).toContain('Add an address');
    expect(dialog.querySelector<HTMLInputElement>('[name="recipientName"]')!.value).toBe('');
  });

  it('ADD-1 an empty book says so and still offers Add', () => {
    const { el } = render(ok([]));

    expect(el.textContent).toContain('no saved addresses');
    expect(el.querySelector('button[data-add]')).toBeTruthy();
  });

  it('ADD-3 a signed-out visitor is told, and no address or add button is shown', () => {
    const { el } = render({ status: 'signedOut' });

    expect(el.querySelector('[role="status"], [role="alert"]')!.textContent).toMatch(/not signed in/i);
    expect(el.querySelector('ul.rp-address-list')).toBeNull();
    expect(el.querySelector('button[data-add]')).toBeNull();
  });

  it('SEO-05 the page is noindex and its response is never cached', () => {
    const init: ResponseInit = { status: 200 };
    render(ok([]), init);

    expect(TestBed.inject(Meta).getTag('name="robots"')!.content).toBe('noindex, nofollow');
    expect(new Headers(init.headers).get('Cache-Control')).toBe('private, no-store');
  });

  it('ADD-7 shows the pager only when there is more than one page', () => {
    const one = render(ok([address(1, true)]));
    expect(one.el.querySelector('nav[aria-label="Pages of addresses"]')).toBeNull();
  });
});
