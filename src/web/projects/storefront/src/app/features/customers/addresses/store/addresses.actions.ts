import { createActionGroup, emptyProps, props } from '@ngrx/store';
import { AddressInput, ChangeResult, City, LoadResult, Province } from '../models/address.model';

export const AddressesActions = createActionGroup({
  source: 'Addresses',
  events: {
    Load: props<{ page: number }>(),
    'Load Done': props<{ result: LoadResult }>(),
    /** Add (no id) or edit (with id). */
    Save: props<{ id: number | null; input: AddressInput }>(),
    Remove: props<{ id: number }>(),
    'Make Default': props<{ id: number }>(),
    'Change Done': props<{ kind: 'save' | 'remove' | 'default'; result: ChangeResult }>(),
    'Clear Feedback': emptyProps(),
    'Load Provinces': emptyProps(),
    'Load Provinces Done': props<{ provinces: readonly Province[] }>(),
    'Load Cities': props<{ provinceId: number }>(),
    'Load Cities Done': props<{ provinceId: number; cities: readonly City[] }>(),
  },
});
