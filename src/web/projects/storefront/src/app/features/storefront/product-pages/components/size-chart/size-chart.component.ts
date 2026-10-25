import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { SizeChart } from '../../models/product-page.model';
import { columnsFor } from './size-chart.format';

/**
 * The size guide (PDP-6, 7). A real table with a caption and header cells that name their column or row. The chosen size is marked in
 * words as well as style. Without a chart the page says so and keeps working.
 */
@Component({
  selector: 'rp-size-chart',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="chart" aria-labelledby="size-guide-heading">
      <h2 id="size-guide-heading" class="rp-eyebrow chart__title">Size guide</h2>
      @if (chart(); as c) {
        <div class="chart__scroll" tabindex="0" role="region" aria-labelledby="size-guide-heading">
          <table>
            <caption>{{ c.name }}, measurements in centimetres</caption>
            <thead>
              <tr>
                <th scope="col">Size</th>
                @for (col of columns(); track col.key) {
                  <th scope="col">{{ col.heading }}</th>
                }
              </tr>
            </thead>
            <tbody>
              @for (row of c.rows; track row.sizeCode) {
                <tr [class.chart__selected]="row.selected" [attr.aria-current]="row.selected ? 'true' : null">
                  <th scope="row">{{ row.sizeName }}@if (row.selected) { <span class="chart__you"> (your size)</span> }</th>
                  @for (col of columns(); track col.key) {
                    <td>{{ col.value(row) ?? '-' }}</td>
                  }
                </tr>
              }
            </tbody>
          </table>
        </div>
      } @else {
        <p class="chart__none">There is no size guide for this piece yet. Ask us on WhatsApp and we will help you choose.</p>
      }
    </section>
  `,
  styles: `
    .chart__title { margin: 0 0 var(--space-3); }
    .chart__scroll { overflow-x: auto; }
    .chart__scroll:focus-visible { outline: 2px solid var(--ink); outline-offset: 2px; }
    table { width: 100%; border-collapse: collapse; font: 400 14px/20px var(--font-sans); }
    caption { text-align: left; padding-bottom: var(--space-2); color: var(--ink-muted); }
    th, td { padding: var(--space-2) var(--space-3); text-align: left; border-bottom: 1px solid var(--line); white-space: nowrap; }
    thead th { font-weight: 500; }
    tbody th { font-weight: 500; }
    .chart__selected th, .chart__selected td { background: var(--surface-sunken, var(--line)); font-weight: 500; }
    .chart__you { font-weight: 400; color: var(--ink-muted); }
    .chart__none { margin: 0; font: 400 15px/24px var(--font-sans); color: var(--ink-muted); }
  `,
})
export class SizeChartComponent {
  readonly chart = input.required<SizeChart | null>();

  protected readonly columns = computed(() => columnsFor(this.chart()?.rows ?? []));
}
