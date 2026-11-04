import { createFeature, createReducer, on } from '@ngrx/store';
import { AddressPage, City, FieldErrors, Province } from '../models/address.model';
import { AddressesActions } from './addresses.actions';

export type AddressesStatus = 'idle' | 'loading' | 'ready' | 'signedOut' | 'error';

export interface AddressesState {
  readonly status: AddressesStatus;
  readonly page: AddressPage | null;
  /** The page being shown or asked for. */
  readonly pageNo: number;
  readonly saving: boolean;
  /** Per-field messages from the last save, shown beside the fields. */
  readonly fieldErrors: FieldErrors;
  /** A rule-level message (the limit, a missing address, a failure) shown with the dialog or the page. */
  readonly message: string | null;
  /** Set when a save succeeded, so the dialog knows to close. Cleared by the next change or by clear-feedback. */
  readonly saved: boolean;
  readonly provinces: readonly Province[];
  readonly cities: Readonly<Record<number, readonly City[]>>;
}

export const initialAddressesState: AddressesState = {
  status: 'idle',
  page: null,
  pageNo: 1,
  saving: false,
  fieldErrors: {},
  message: null,
  saved: false,
  provinces: [],
  cities: {},
};

export const addressesFeature = createFeature({
  name: 'addresses',
  reducer: createReducer(
    initialAddressesState,
    // The page on screen stays until the new one arrives, so paging and saving do not flash blank.
    on(AddressesActions.load, (s, { page }): AddressesState => ({ ...s, status: s.page ? s.status : 'loading', pageNo: page })),
    on(AddressesActions.loadDone, (s, { result }): AddressesState => {
      switch (result.status) {
        case 'ok':
          return { ...s, status: 'ready', page: result.page, pageNo: result.page.page };
        case 'signedOut':
          return { ...s, status: 'signedOut', page: null };
        default:
          return { ...s, status: 'error' };
      }
    }),
    on(AddressesActions.save, AddressesActions.remove, AddressesActions.makeDefault, (s): AddressesState => ({ ...s, saving: true, fieldErrors: {}, message: null, saved: false })),
    on(AddressesActions.changeDone, (s, { kind, result }): AddressesState => {
      const base = { ...s, saving: false };
      switch (result.status) {
        case 'ok':
          return { ...base, saved: kind === 'save', message: null, fieldErrors: {} };
        case 'invalid':
          return { ...base, fieldErrors: result.errors, message: null };
        case 'refused':
          return { ...base, message: result.message };
        case 'notFound':
          return { ...base, message: 'That address is no longer there. The list has been refreshed.' };
        case 'signedOut':
          return { ...base, status: 'signedOut', page: null };
        default:
          return { ...base, message: 'Something went wrong. Please try again.' };
      }
    }),
    on(AddressesActions.clearFeedback, (s): AddressesState => ({ ...s, fieldErrors: {}, message: null, saved: false })),
    on(AddressesActions.loadProvincesDone, (s, { provinces }): AddressesState => ({ ...s, provinces })),
    on(AddressesActions.loadCitiesDone, (s, { provinceId, cities }): AddressesState => ({ ...s, cities: { ...s.cities, [provinceId]: cities } })),
  ),
});
