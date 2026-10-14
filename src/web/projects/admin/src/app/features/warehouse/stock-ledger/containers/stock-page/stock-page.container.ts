import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { Store } from '@ngrx/store';
import { PaginationComponent, SectionNavComponent, inputValue } from '@shared';
import { MovementListComponent } from '../../components/movement-list/movement-list.component';
import { StockTableComponent } from '../../components/stock-table/stock-table.component';
import { StockVariantRowDto } from '../../models';
import { WAREHOUSE_LINKS } from '../../stock-ledger.constants';
import {
  StockPageActions,
  selectError,
  selectHistoryFor,
  selectLoading,
  selectMovements,
  selectMovementsLoading,
  selectMovementsPage,
  selectMovementsTotal,
  selectPage,
  selectPageSize,
  selectRows,
  selectSearchText,
  selectTotal,
} from '../../store';

/** Stock (docs/specs/warehouse/stock-ledger/spec.md): what is on the shelves, where, and how it got there (LED-5..9). Read only (LED-10). */
@Component({
  selector: 'app-stock-page',
  imports: [SectionNavComponent, StockTableComponent, MovementListComponent, PaginationComponent],
  templateUrl: './stock-page.container.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StockPageContainer {
  private readonly store = inject(Store);

  readonly links = WAREHOUSE_LINKS;
  readonly rows = this.store.selectSignal(selectRows);
  readonly total = this.store.selectSignal(selectTotal);
  readonly currentPage = this.store.selectSignal(selectPage);
  readonly pageSize = this.store.selectSignal(selectPageSize);
  readonly loading = this.store.selectSignal(selectLoading);
  readonly error = this.store.selectSignal(selectError);
  readonly searchText = this.store.selectSignal(selectSearchText);
  readonly historyFor = this.store.selectSignal(selectHistoryFor);
  readonly movements = this.store.selectSignal(selectMovements);
  readonly movementsTotal = this.store.selectSignal(selectMovementsTotal);
  readonly movementsPage = this.store.selectSignal(selectMovementsPage);
  readonly movementsLoading = this.store.selectSignal(selectMovementsLoading);

  constructor() {
    this.store.dispatch(StockPageActions.opened());
  }

  onSearchInput(event: Event): void {
    this.store.dispatch(StockPageActions.searchChanged({ searchText: inputValue(event) }));
  }

  refresh(): void {
    this.store.dispatch(StockPageActions.refreshRequested());
  }

  showHistory(variant: StockVariantRowDto): void {
    this.store.dispatch(StockPageActions.historyRequested({ variant }));
  }

  changeHistoryPage(page: number): void {
    this.store.dispatch(StockPageActions.historyPageChanged({ page }));
  }

  closeHistory(): void {
    this.store.dispatch(StockPageActions.historyClosed());
  }

  changePage(page: number): void {
    this.store.dispatch(StockPageActions.pageChanged({ page }));
  }

  changePageSize(size: number): void {
    this.store.dispatch(StockPageActions.pageSizeChanged({ pageSize: size }));
  }
}
