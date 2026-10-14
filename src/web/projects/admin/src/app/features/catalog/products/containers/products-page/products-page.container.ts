import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Store } from '@ngrx/store';
import { CatalogApiService } from '@features/catalog/styles';
import { ReferenceLookupActions, selectLookup } from '@features/reference/lookups';
import { ActingPersonService, PaginationComponent, SectionNavComponent, fetchAllPages, inputValue, selectNumberOrNull } from '@shared';
import { ProductCreateFormComponent, StyleChoice } from '../../components/product-create-form/product-create-form.component';
import { ProductTableComponent } from '../../components/product-table/product-table.component';
import { CreateProductValue, PRODUCT_STATUSES } from '../../models';
import { CATALOG_LINKS } from '../../products.constants';
import {
  ProductsPageActions,
  selectCreateError,
  selectCreateOpen,
  selectCreating,
  selectListError,
  selectListLoading,
  selectNotice,
  selectPage,
  selectPageSize,
  selectRows,
  selectSearch,
  selectStatusId,
  selectTotal,
} from '../../store';

const LOOKUP_TYPES = ['genders', 'categories', 'age-brackets'] as const;

/** Products (docs/specs/catalog/products/spec.md): the paged list with filters, and creating a product from a style (PRD-1, PRD-11). */
@Component({
  selector: 'app-products-page',
  imports: [SectionNavComponent, ProductTableComponent, ProductCreateFormComponent, PaginationComponent],
  templateUrl: './products-page.container.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProductsPageContainer {
  private readonly store = inject(Store);
  private readonly styleApi = inject(CatalogApiService);
  private readonly actingPerson = inject(ActingPersonService);
  private readonly destroyRef = inject(DestroyRef);

  readonly links = CATALOG_LINKS;
  readonly statuses = PRODUCT_STATUSES;
  readonly rows = this.store.selectSignal(selectRows);
  readonly total = this.store.selectSignal(selectTotal);
  readonly currentPage = this.store.selectSignal(selectPage);
  readonly pageSize = this.store.selectSignal(selectPageSize);
  readonly loading = this.store.selectSignal(selectListLoading);
  readonly error = this.store.selectSignal(selectListError);
  readonly search = this.store.selectSignal(selectSearch);
  readonly statusId = this.store.selectSignal(selectStatusId);
  readonly createOpen = this.store.selectSignal(selectCreateOpen);
  readonly creating = this.store.selectSignal(selectCreating);
  readonly createError = this.store.selectSignal(selectCreateError);
  readonly notice = this.store.selectSignal(selectNotice);
  readonly genders = this.store.selectSignal(selectLookup('genders'));
  readonly categories = this.store.selectSignal(selectLookup('categories'));
  readonly ageBrackets = this.store.selectSignal(selectLookup('age-brackets'));
  readonly defaultPerson = this.actingPerson.name;
  readonly styles = signal<readonly StyleChoice[]>([]);

  constructor() {
    this.store.dispatch(ProductsPageActions.opened());
    for (const typeKey of LOOKUP_TYPES) {
      this.store.dispatch(ReferenceLookupActions.requested({ typeKey, includeInactive: false }));
    }
  }

  onSearch(event: Event): void {
    this.store.dispatch(ProductsPageActions.searchChanged({ search: inputValue(event) }));
  }

  onStatus(event: Event): void {
    this.store.dispatch(ProductsPageActions.statusChanged({ statusId: selectNumberOrNull(event) }));
  }

  refresh(): void {
    this.store.dispatch(ProductsPageActions.refreshRequested());
  }

  changePage(page: number): void {
    this.store.dispatch(ProductsPageActions.pageChanged({ page }));
  }

  changePageSize(pageSize: number): void {
    this.store.dispatch(ProductsPageActions.pageSizeChanged({ pageSize }));
  }

  /** The styles to choose from are read when the form opens (a pick-list needs every active style, CONVENTIONS section 4). */
  openCreate(): void {
    this.store.dispatch(ProductsPageActions.createOpened());
    fetchAllPages((page, pageSize) => this.styleApi.search({ searchText: '', categoryId: null, activeOnly: true, page, pageSize }))
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((all) => this.styles.set(all.map((style) => ({ id: style.id, code: style.code, name: style.name, categoryId: style.categoryId }))));
  }

  closeCreate(): void {
    this.store.dispatch(ProductsPageActions.createClosed());
  }

  create(value: CreateProductValue): void {
    this.actingPerson.remember(value.actingPerson);
    this.store.dispatch(ProductsPageActions.createSubmitted({ value }));
  }

  dismissNotice(): void {
    this.store.dispatch(ProductsPageActions.noticeDismissed());
  }
}
