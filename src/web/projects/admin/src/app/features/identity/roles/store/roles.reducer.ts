import { createReducer, on } from '@ngrx/store';
import { RolesApiActions, RolesPageActions } from './roles.actions';
import { initialRolesState } from './roles.state';

export const rolesReducer = createReducer(
  initialRolesState,
  on(RolesPageActions.opened, () => ({ ...initialRolesState, loading: true })),
  on(RolesApiActions.loadSucceeded, (state, { matrix }) => ({ ...state, matrix, loading: false, error: null })),
  on(RolesApiActions.loadFailed, (state, { error }) => ({ ...state, matrix: null, loading: false, error })),
);
