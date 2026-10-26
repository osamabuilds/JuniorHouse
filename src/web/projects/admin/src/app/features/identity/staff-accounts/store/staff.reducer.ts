import { createReducer, on } from '@ngrx/store';
import { StaffApiActions, StaffPageActions } from './staff.actions';
import { initialStaffState } from './staff.state';

export const staffReducer = createReducer(
  initialStaffState,

  // The store outlives the page, so opening it starts from a clean slate (and drops any password still held).
  on(StaffPageActions.opened, () => ({ ...initialStaffState, loading: true })),
  on(StaffPageActions.searchChanged, (state, { searchText }) => ({ ...state, searchText, page: 1, loading: true, error: null })),
  on(StaffPageActions.activeOnlyToggled, (state) => ({ ...state, activeOnly: !state.activeOnly, page: 1, loading: true, error: null })),
  on(StaffPageActions.pageChanged, (state, { page }) => ({ ...state, page, loading: true, error: null })),
  on(StaffPageActions.pageSizeChanged, (state, { pageSize }) => ({ ...state, pageSize, page: 1, loading: true, error: null })),
  on(StaffPageActions.createStarted, (state) => ({ ...state, mode: 'create' as const, selected: null, error: null, oneTimePassword: null })),
  on(StaffPageActions.backToListClicked, (state) => ({ ...state, mode: 'list' as const, selected: null, error: null, oneTimePassword: null, loading: true })),
  on(StaffPageActions.staffSelected, (state) => ({ ...state, mode: 'detail' as const, selected: null, error: null, oneTimePassword: null })),
  on(StaffPageActions.formInvalid, (state, { error }) => ({ ...state, error })),
  on(StaffPageActions.createSubmitted, (state) => ({ ...state, saving: true, error: null })),
  on(
    StaffPageActions.deactivateClicked,
    StaffPageActions.reactivateClicked,
    StaffPageActions.resetPasswordClicked,
    (state) => ({ ...state, saving: true, error: null }),
  ),
  on(StaffPageActions.passwordDismissed, (state) => ({ ...state, oneTimePassword: null })),

  on(StaffApiActions.loadStaffSucceeded, (state, { result }) => ({
    ...state,
    staff: result.items,
    total: result.totalCount,
    page: result.page,
    loading: false,
  })),
  on(StaffApiActions.loadStaffFailed, (state, { error }) => ({ ...state, error, loading: false })),
  on(StaffApiActions.loadRolesSucceeded, (state, { roles }) => ({ ...state, roles })),
  on(StaffApiActions.loadRolesFailed, (state, { error }) => ({ ...state, error })),
  on(StaffApiActions.loadDetailSucceeded, (state, { staff }) => ({ ...state, selected: staff })),
  on(StaffApiActions.loadDetailFailed, (state, { error }) => ({ ...state, error })),
  // After a create the person stays on the screen that shows the password, in list mode behind it.
  on(StaffApiActions.createSucceeded, (state, { password }) => ({
    ...state,
    saving: false,
    mode: 'list' as const,
    oneTimePassword: password,
    loading: true,
    error: null,
  })),
  on(StaffApiActions.resetSucceeded, (state, { password }) => ({ ...state, saving: false, oneTimePassword: password, error: null })),
  on(StaffApiActions.statusChangeSucceeded, (state) => ({ ...state, saving: false, error: null })),
  on(StaffApiActions.actionFailed, (state, { error }) => ({ ...state, saving: false, error })),
);
