import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { ProductSummary } from '../../models/product.model';

@Component({
  selector: 'app-product-card',
  imports: [RouterLink, DecimalPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './product-card.component.html',
  styleUrl: './product-card.component.scss',
})
export class ProductCardComponent {
  readonly product = input.required<ProductSummary>();
  /** Alternates the tile tone so a row of placeholders reads as a set. */
  readonly alt = input(false);

  protected readonly link = computed(() => {
    const p = this.product();
    return ['/', p.category, p.type, p.slug];
  });
}
