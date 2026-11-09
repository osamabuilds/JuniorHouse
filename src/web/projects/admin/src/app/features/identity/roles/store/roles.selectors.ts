import { createFeatureSelector, createSelector } from '@ngrx/store';
import { ROLES_FEATURE_KEY, RolesState } from './roles.state';

const selectRoles = createFeatureSelector<RolesState>(ROLES_FEATURE_KEY);

export const selectRoleMatrix = createSelector(selectRoles, (state) => state.matrix);
export const selectRolesLoading = createSelector(selectRoles, (state) => state.loading);
export const selectRolesError = createSelector(selectRoles, (state) => state.error);
