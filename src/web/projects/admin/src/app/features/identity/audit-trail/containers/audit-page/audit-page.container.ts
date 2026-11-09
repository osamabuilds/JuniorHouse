import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { Store } from '@ngrx/store';
import { PaginationComponent } from '@shared';
import { AuditDetailComponent } from '../../components/audit-detail/audit-detail.component';
import { AuditFiltersComponent } from '../../components/audit-filters/audit-filters.component';
import { AuditListComponent } from '../../components/audit-list/audit-list.component';
import { AuditEntryDto, AuditFormValue } from '../../models';
import {
  AuditPageActions,
  selectAuditEntries,
  selectAuditError,
  selectAuditForm,
  selectAuditLoading,
  selectAuditPage,
  selectAuditPageSize,
  selectAuditSelected,
  selectAuditTotal,
} from '../../store';

/** The audit trail (identity/audit-trail, audit.read): who changed what and when, across every module. Read-only. */
@Component({
  selector: 'app-audit-page',
  imports: [AuditFiltersComponent, AuditListComponent, AuditDetailComponent, PaginationComponent],
  template: `
    <h1>Audit trail</h1>
    <p class="page-intro">
      Who changed what, and when, in every part of Romp. Entries name the record and the columns that changed, never the values. An entry
      appears a few seconds after the change.
    </p>

    @if (error()) {
      <p class="banner banner--error" role="alert">{{ error()?.message }}</p>
    }

    @if (selected(); as entry) {
      <app-audit-detail [entry]="entry" (closed)="closeDetail()" />
    } @else {
      <app-audit-filters [value]="form()" (applied)="apply($event)" />
      <app-audit-list [entries]="entries()" [loading]="loading()" (selected)="open($event)" />
      <app-pagination [total]="total()" [page]="currentPage()" [pageSize]="pageSize()" (pageChanged)="changePage($event)" (pageSizeChanged)="changePageSize($event)" />
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AuditPageContainer {
  private readonly store = inject(Store);

  readonly entries = this.store.selectSignal(selectAuditEntries);
  readonly total = this.store.selectSignal(selectAuditTotal);
  readonly currentPage = this.store.selectSignal(selectAuditPage);
  readonly pageSize = this.store.selectSignal(selectAuditPageSize);
  readonly form = this.store.selectSignal(selectAuditForm);
  readonly selected = this.store.selectSignal(selectAuditSelected);
  readonly loading = this.store.selectSignal(selectAuditLoading);
  readonly error = this.store.selectSignal(selectAuditError);

  constructor() {
    this.store.dispatch(AuditPageActions.opened());
  }

  apply(form: AuditFormValue): void {
    this.store.dispatch(AuditPageActions.filtersApplied({ form }));
  }

  changePage(page: number): void {
    this.store.dispatch(AuditPageActions.pageChanged({ page }));
  }

  changePageSize(pageSize: number): void {
    this.store.dispatch(AuditPageActions.pageSizeChanged({ pageSize }));
  }

  open(entry: AuditEntryDto): void {
    this.store.dispatch(AuditPageActions.entrySelected({ entry }));
  }

  closeDetail(): void {
    this.store.dispatch(AuditPageActions.detailClosed());
  }
}
