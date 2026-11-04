import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { LookupDto } from '@features/reference/lookups';
import { SizeChartRowDto } from '../../models';
import { HasPermissionDirective } from '@core/auth';

/** The size chart list (CHT-8): one row per category and audience, with its number of sizes and an Edit button. Presentational only. */
@Component({
  imports: [HasPermissionDirective],
  selector: 'app-size-chart-table',
  template: `
    @if (loading()) {
      <p class="state-message">Loading…</p>
    } @else if (charts().length === 0) {
      <p class="state-message">No size charts yet. Products without one can still be published.</p>
    } @else {
      <table class="responsive">
        <caption class="sr-only">Size charts</caption>
        <thead>
          <tr>
            <th scope="col">Chart</th>
            <th scope="col">Category</th>
            <th scope="col">For</th>
            <th scope="col">Sizes</th>
            <th scope="col">Actions</th>
          </tr>
        </thead>
        <tbody>
          @for (chart of charts(); track chart.id) {
            <tr>
              <th scope="row" data-label="Chart">{{ chart.name }}</th>
              <td data-label="Category">{{ nameOf(categories(), chart.categoryId) }}</td>
              <td data-label="For">{{ nameOf(genders(), chart.genderId) }}</td>
              <td data-label="Sizes">{{ chart.lineCount }}</td>
              <td data-label="Actions">
                <button *hasPermission="'catalog.write'" type="button" class="button button--small" (click)="edit.emit(chart.id)">Edit</button>
              </td>
            </tr>
          }
        </tbody>
      </table>
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SizeChartTableComponent {
  readonly charts = input.required<readonly SizeChartRowDto[]>();
  readonly categories = input.required<readonly LookupDto[]>();
  readonly genders = input.required<readonly LookupDto[]>();
  readonly loading = input(false);

  readonly edit = output<number>();

  nameOf(list: readonly LookupDto[], id: number): string {
    return list.find((item) => item.id === id)?.name ?? `#${id}`;
  }
}
