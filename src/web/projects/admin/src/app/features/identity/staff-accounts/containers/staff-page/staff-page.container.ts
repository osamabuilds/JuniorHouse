import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { Store } from '@ngrx/store';
import { inputValue, PaginationComponent } from '@shared';
import { OneTimePasswordComponent } from '../../components/one-time-password/one-time-password.component';
import { StaffCreateComponent } from '../../components/staff-create/staff-create.component';
import { StaffDetailComponent } from '../../components/staff-detail/staff-detail.component';
import { StaffListComponent } from '../../components/staff-list/staff-list.component';
import { CreateStaffValue, StaffDto } from '../../models';
import {
  selectOneTimePassword,
  selectSelectedStaff,
  selectStaff,
  selectStaffActiveOnly,
  selectStaffError,
  selectStaffLoading,
  selectStaffMode,
  selectStaffPage,
  selectStaffPageSize,
  selectStaffRoles,
  selectStaffSaving,
  selectStaffSearchText,
  selectStaffTotal,
  StaffPageActions,
} from '../../store';

/** Staff accounts (identity/staff-accounts): list and search, create, open one to deactivate, reactivate or reset its password. */
@Component({
  selector: 'app-staff-page',
  imports: [StaffListComponent, StaffCreateComponent, StaffDetailComponent, OneTimePasswordComponent, PaginationComponent],
  templateUrl: './staff-page.container.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StaffPageContainer {
  private readonly store = inject(Store);

  readonly staff = this.store.selectSignal(selectStaff);
  readonly total = this.store.selectSignal(selectStaffTotal);
  readonly currentPage = this.store.selectSignal(selectStaffPage);
  readonly pageSize = this.store.selectSignal(selectStaffPageSize);
  readonly loading = this.store.selectSignal(selectStaffLoading);
  readonly error = this.store.selectSignal(selectStaffError);
  readonly mode = this.store.selectSignal(selectStaffMode);
  readonly selected = this.store.selectSignal(selectSelectedStaff);
  readonly roles = this.store.selectSignal(selectStaffRoles);
  readonly saving = this.store.selectSignal(selectStaffSaving);
  readonly searchText = this.store.selectSignal(selectStaffSearchText);
  readonly activeOnly = this.store.selectSignal(selectStaffActiveOnly);
  readonly oneTimePassword = this.store.selectSignal(selectOneTimePassword);

  constructor() {
    this.store.dispatch(StaffPageActions.opened());
  }

  onSearchInput(event: Event): void {
    this.store.dispatch(StaffPageActions.searchChanged({ searchText: inputValue(event) }));
  }

  toggleActiveOnly(): void {
    this.store.dispatch(StaffPageActions.activeOnlyToggled());
  }

  changePage(page: number): void {
    this.store.dispatch(StaffPageActions.pageChanged({ page }));
  }

  changePageSize(pageSize: number): void {
    this.store.dispatch(StaffPageActions.pageSizeChanged({ pageSize }));
  }

  startCreate(): void {
    this.store.dispatch(StaffPageActions.createStarted());
  }

  open(person: StaffDto): void {
    this.store.dispatch(StaffPageActions.staffSelected({ id: person.id }));
  }

  backToList(): void {
    this.store.dispatch(StaffPageActions.backToListClicked());
  }

  create(value: CreateStaffValue): void {
    this.store.dispatch(StaffPageActions.createSubmitted({ value }));
  }

  deactivate(person: StaffDto): void {
    this.store.dispatch(StaffPageActions.deactivateClicked({ id: person.id }));
  }

  reactivate(person: StaffDto): void {
    this.store.dispatch(StaffPageActions.reactivateClicked({ id: person.id }));
  }

  resetPassword(person: StaffDto): void {
    this.store.dispatch(StaffPageActions.resetPasswordClicked({ id: person.id }));
  }

  saveRoles(person: StaffDto, roleIds: readonly number[]): void {
    this.store.dispatch(StaffPageActions.rolesSubmitted({ id: person.id, roleIds }));
  }

  dismissPassword(): void {
    this.store.dispatch(StaffPageActions.passwordDismissed());
  }
}
