import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ProductRowDto } from '../../models';

/**
 * The product list (PRD-11): one row per product with its style, status and how many of its planned variants can be bought now.
 * The name is a real link to the editor. Presentational only.
 */
@Component({
  selector: 'app-product-table',
  imports: [DatePipe, RouterLink],
  template: `
    @if (loading()) {
      <p class="state-message">Loading…</p>
    } @else if (products().length === 0) {
      <p class="state-message">No products found.</p>
    } @else {
      <table class="responsive">
        <caption class="sr-only">Products</caption>
        <thead>
          <tr>
            <th scope="col">Product</th>
            <th scope="col">Style</th>
            <th scope="col">Status</th>
            <th scope="col">Available variants</th>
            <th scope="col">Published</th>
          </tr>
        </thead>
        <tbody>
          @for (product of products(); track product.id) {
            <tr>
              <td data-label="Product"><a [routerLink]="['/catalog/products', product.id]">{{ product.name }}</a></td>
              <td data-label="Style">{{ product.styleCode }}</td>
              <td data-label="Status">
                <span class="badge" [class.badge--muted]="product.status !== 'Published'">{{ product.status }}</span>
              </td>
              <td data-label="Available variants">{{ product.sellableVariants }} of {{ product.plannedVariants }}</td>
              <td data-label="Published">
                @if (product.publishedAt) {
                  {{ product.publishedAt | date: 'd MMM y' }}
                } @else {
                  <span class="badge badge--muted">Not yet</span>
                }
              </td>
            </tr>
          }
        </tbody>
      </table>
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProductTableComponent {
  readonly products = input.required<readonly ProductRowDto[]>();
  readonly loading = input(false);
}
