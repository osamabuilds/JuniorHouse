import { addressesFeature } from './addresses.reducer';

export const {
  selectStatus: selectAddressesStatus,
  selectPage: selectAddressPage,
  selectSaving: selectAddressSaving,
  selectFieldErrors: selectAddressFieldErrors,
  selectMessage: selectAddressMessage,
  selectSaved: selectAddressSaved,
  selectProvinces,
  selectCities: selectCitiesByProvince,
} = addressesFeature;
