import { ChangeDetectionStrategy, Component, effect, inject, input } from '@angular/core';
import { Store } from '@ngrx/store';
import { CATALOG_LINKS } from '@features/catalog/products';
import { ReferenceLookupActions, selectLookup } from '@features/reference/lookups';
import { ActingPersonService, PaginationComponent, SectionNavComponent } from '@shared';
import { SizeChartEditorComponent } from '../../components/size-chart-editor/size-chart-editor.component';
import { SizeChartTableComponent } from '../../components/size-chart-table/size-chart-table.component';
import { SizeChartFormValue } from '../../models';
import {
  SizeChartsPageActions,
  selectChart,
  selectError,
  selectFormError,
  selectLoading,
  selectMode,
  selectNotice,
  selectPage,
  selectPageSize,
  selectRows,
  selectSaving,
  selectTotal,
} from '../../store';

const LOOKUP_TYPES = ['categories', 'genders', 'sizes'] as const;

/** Size charts (docs/specs/catalog/size-charts/spec.md): the paged list and the chart editor (CHT-1..8). `/size-charts/:id` opens a chart to edit. */
@Component({
  selector: 'app-size-charts-page',
  imports: [SectionNavComponent, SizeChartTableComponent, SizeChartEditorComponent, PaginationComponent],
  templateUrl: './size-charts-page.container.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SizeChartsPageContainer {
  private readonly store = inject(Store);
  private readonly actingPerson = inject(ActingPersonService);

  /** Set when the route is `size-charts/:id`. */
  readonly id = input<string>();

  readonly links = CATALOG_LINKS;
  readonly rows = this.store.selectSignal(selectRows);
  readonly total = this.store.selectSignal(selectTotal);
  readonly currentPage = this.store.selectSignal(selectPage);
  readonly pageSize = this.store.selectSignal(selectPageSize);
  readonly loading = this.store.selectSignal(selectLoading);
  readonly error = this.store.selectSignal(selectError);
  readonly mode = this.store.selectSignal(selectMode);
  readonly chart = this.store.selectSignal(selectChart);
  readonly saving = this.store.selectSignal(selectSaving);
  readonly formError = this.store.selectSignal(selectFormError);
  readonly notice = this.store.selectSignal(selectNotice);
  readonly categories = this.store.selectSignal(selectLookup('categories'));
  readonly genders = this.store.selectSignal(selectLookup('genders'));
  readonly sizes = this.store.selectSignal(selectLookup('sizes'));
  readonly defaultPerson = this.actingPerson.name;

  constructor() {
    this.store.dispatch(SizeChartsPageActions.opened());
    for (const typeKey of LOOKUP_TYPES) {
      this.store.dispatch(ReferenceLookupActions.requested({ typeKey, includeInactive: false }));
    }

    effect(() => {
      const id = Number(this.id());
      if (this.id() !== undefined && Number.isFinite(id)) {
        this.store.dispatch(SizeChartsPageActions.editOpened({ id }));
      }
    });
  }

  startCreate(): void {
    this.store.dispatch(SizeChartsPageActions.createStarted());
  }

  edit(id: number): void {
    this.store.dispatch(SizeChartsPageActions.editOpened({ id }));
  }

  close(): void {
    this.store.dispatch(SizeChartsPageActions.editorClosed());
  }

  save(value: SizeChartFormValue & { readonly person: string }): void {
    this.actingPerson.remember(value.person);
    const { person, ...form } = value;
    this.store.dispatch(
      this.chart() ? SizeChartsPageActions.updateSubmitted({ value: form, person }) : SizeChartsPageActions.createSubmitted({ value: form, person }),
    );
  }

  remove(person: string): void {
    const chart = this.chart();
    if (chart && person.trim().length >= 2) {
      this.actingPerson.remember(person);
      this.store.dispatch(SizeChartsPageActions.deleteConfirmed({ id: chart.id, person: person.trim() }));
    }
  }

  changePage(page: number): void {
    this.store.dispatch(SizeChartsPageActions.pageChanged({ page }));
  }

  changePageSize(pageSize: number): void {
    this.store.dispatch(SizeChartsPageActions.pageSizeChanged({ pageSize }));
  }

  dismissNotice(): void {
    this.store.dispatch(SizeChartsPageActions.noticeDismissed());
  }
}
