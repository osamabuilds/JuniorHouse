import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ProductSizeChartDto, SizeChartLineDto } from '../../models';

/** "12 to 24" for a range, "12" for one end, an en dash for nothing: ages are in months, the rest as given. */
export function rangeText(min: number | null, max: number | null): string {
  if (min === null && max === null) {
    return '–';
  }
  return min !== null && max !== null ? `${min} to ${max}` : String(min ?? max);
}

/**
 * The Size chart tab (CHT-1, CHT-3): the chart that applies to this product, as a real table with header cells, read-only, with a link
 * to edit it. A product with no chart for its category can still be published; the tab says so.
 */
@Component({
  selector: 'app-size-chart-view',
  imports: [RouterLink],
  template: `
    @if (!chart()) {
      <p class="state-message">Loading…</p>
    } @else if (!chart()!.found) {
      <p class="banner banner--info" role="status">
        There is no size chart for this category and audience yet. The product can still be published; the shop will say "size chart coming
        soon". <a routerLink="/catalog/size-charts">Add a size chart</a>.
      </p>
    } @else {
      <p>
        Chart: <strong>{{ chart()!.name }}</strong> (<a [routerLink]="['/catalog/size-charts', chart()!.chartId]">edit</a>). Measurements are in
        {{ chart()!.unit }}, weight in kg, age in months.
      </p>
      <table class="responsive">
        <caption class="sr-only">Size chart</caption>
        <thead>
          <tr>
            <th scope="col">Size</th>
            <th scope="col">Age (months)</th>
            <th scope="col">Height (cm)</th>
            <th scope="col">Weight (kg)</th>
            <th scope="col">Chest (cm)</th>
            <th scope="col">Waist (cm)</th>
            <th scope="col">Length (cm)</th>
          </tr>
        </thead>
        <tbody>
          @for (line of chart()!.lines; track line.sizeId) {
            <tr>
              <th scope="row" data-label="Size">{{ line.sizeName }}</th>
              <td data-label="Age (months)">{{ range(line.ageMinMonths, line.ageMaxMonths) }}</td>
              <td data-label="Height (cm)">{{ range(line.heightMinCm, line.heightMaxCm) }}</td>
              <td data-label="Weight (kg)">{{ range(line.weightMinKg, line.weightMaxKg) }}</td>
              <td data-label="Chest (cm)">{{ single(line, 'chestCm') }}</td>
              <td data-label="Waist (cm)">{{ single(line, 'waistCm') }}</td>
              <td data-label="Length (cm)">{{ single(line, 'lengthCm') }}</td>
            </tr>
          }
        </tbody>
      </table>
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SizeChartViewComponent {
  readonly chart = input.required<ProductSizeChartDto | null>();

  range = rangeText;

  single(line: SizeChartLineDto, field: 'chestCm' | 'waistCm' | 'lengthCm'): string {
    return line[field] === null ? '–' : String(line[field]);
  }
}
