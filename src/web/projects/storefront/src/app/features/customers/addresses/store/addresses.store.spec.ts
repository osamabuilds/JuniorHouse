import { TestBed } from '@angular/core/testing';
import { provideEffects } from '@ngrx/effects';
import { Store, provideStore } from '@ngrx/store';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { Address, AddressPage, ChangeResult, EMPTY_INPUT, LoadResult } from '../models/address.model';
import { AddressesApi } from '../services/addresses-api.service';
import { AddressesActions } from './addresses.actions';
import { AddressesEffects } from './addresses.effects';
import { addressesFeature } from './addresses.reducer';

const address = (id: number, isDefault = false): Address => ({
  id, recipientName: `Home ${id}`, phone: '+923001234567', provinceId: 1, provinceName: 'Punjab', cityId: 2, cityName: 'Lahore',
  lineOne: 'House 12, Street 4', lineTwo: null, postcode: null, isDefault,
});
const page = (items: Address[], no = 1, totalPages = 1): AddressPage => ({ items, page: no, pageSize: 10, totalCount: items.length, totalPages });

describe('addresses store', () => {
  const list = vi.fn<(p: number) => LoadResult>();
  const create = vi.fn<() => ChangeResult>();
  const remove = vi.fn<() => ChangeResult>();
  const cities = vi.fn((id: number) => [{ id: 1, code: 'X', name: 'City', provinceId: id }]);

  beforeEach(() => {
    list.mockReset().mockReturnValue({ status: 'ok', page: page([address(1, true)]) });
    create.mockReset().mockReturnValue({ status: 'ok' });
    remove.mockReset().mockReturnValue({ status: 'ok' });
    cities.mockClear();
    TestBed.configureTestingModule({
      providers: [
        provideStore({ [addressesFeature.name]: addressesFeature.reducer }),
        provideEffects(AddressesEffects),
        {
          provide: AddressesApi,
          useValue: {
            list: (p: number) => of(list(p)),
            create: () => of(create()),
            update: () => of(create()),
            remove: () => of(remove()),
            makeDefault: () => of({ status: 'ok' }),
            provinces: () => of([{ id: 1, code: 'PB', name: 'Punjab' }]),
            cities: (id: number) => of(cities(id)),
          },
        },
      ],
    });
  });

  const state = () => TestBed.inject(Store).selectSignal(addressesFeature.selectAddressesState)();

  it('ADD-1 loading puts the page in the store and marks it ready', () => {
    TestBed.inject(Store).dispatch(AddressesActions.load({ page: 1 }));
    expect(state().status).toBe('ready');
    expect(state().page?.items.map((a) => a.id)).toEqual([1]);
  });

  it('ADD-3 a signed-out answer clears the page and says so', () => {
    list.mockReturnValue({ status: 'signedOut' });
    TestBed.inject(Store).dispatch(AddressesActions.load({ page: 1 }));
    expect(state().status).toBe('signedOut');
    expect(state().page).toBeNull();
  });

  it('ADD-1 a saved address closes the dialog (saved) and the list is read again', () => {
    const store = TestBed.inject(Store);
    store.dispatch(AddressesActions.load({ page: 1 }));
    list.mockClear();
    store.dispatch(AddressesActions.save({ id: null, input: EMPTY_INPUT }));
    expect(state().saved).toBe(true);
    expect(state().saving).toBe(false);
    expect(list).toHaveBeenCalledTimes(1);
  });

  it('ADD-4 ADD-5 field errors are kept for the form and the dialog stays open', () => {
    create.mockReturnValue({ status: 'invalid', errors: { phone: 'Enter a Pakistani phone number such as 0300 1234567.' } });
    TestBed.inject(Store).dispatch(AddressesActions.save({ id: null, input: EMPTY_INPUT }));
    expect(state().fieldErrors['phone']).toContain('Pakistani phone');
    expect(state().saved).toBe(false);
  });

  it('ADD-6 a refusal shows its message', () => {
    create.mockReturnValue({ status: 'refused', message: 'You can save up to 10 addresses. Remove one to add another.' });
    TestBed.inject(Store).dispatch(AddressesActions.save({ id: null, input: EMPTY_INPUT }));
    expect(state().message).toContain('up to 10');
  });

  it('ADD-1 removing the only address on a later page steps back to the previous page', () => {
    const store = TestBed.inject(Store);
    list.mockImplementation((p) => ({ status: 'ok', page: p === 2 ? page([], 2, 1) : page([address(1, true)], 1, 1) }));
    store.dispatch(AddressesActions.load({ page: 2 }));
    expect(state().pageNo).toBe(1);
    expect(state().page?.items.length).toBe(1);
  });

  it('ADD-4 each province is asked for its cities once, however often it is chosen', () => {
    const store = TestBed.inject(Store);
    store.dispatch(AddressesActions.loadCities({ provinceId: 3 }));
    store.dispatch(AddressesActions.loadCities({ provinceId: 3 }));
    store.dispatch(AddressesActions.loadCities({ provinceId: 0 }));
    expect(cities).toHaveBeenCalledTimes(1);
    expect(state().cities[3]?.length).toBe(1);
  });
});
