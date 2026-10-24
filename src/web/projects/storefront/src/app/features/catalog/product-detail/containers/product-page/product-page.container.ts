import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute } from '@angular/router';
import { map } from 'rxjs';
import { BreadcrumbsComponent } from '../../../../../shared/components/breadcrumbs/breadcrumbs.component';
import { ProductCardComponent } from '../../../../../shared/components/product-card/product-card.component';
import {
  CATEGORY_NAMES,
  SAMPLE_PRODUCTS,
  SAMPLE_SIZES,
  titleCase,
} from '../../../catalog.sample-data';

@Component({
  selector: 'app-product-page',
  imports: [DecimalPipe, BreadcrumbsComponent, ProductCardComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './product-page.container.html',
  styleUrl: './product-page.container.scss',
})
export class ProductPageContainer {
  private readonly route = inject(ActivatedRoute);

  private readonly params = toSignal(
    this.route.paramMap.pipe(
      map((p) => ({
        category: p.get('category') ?? '',
        type: p.get('type') ?? '',
        slug: p.get('slug') ?? '',
      })),
    ),
    { initialValue: { category: '', type: '', slug: '' } },
  );

  protected readonly product = computed(() => {
    const { slug } = this.params();
    return SAMPLE_PRODUCTS.find((p) => p.slug === slug) ?? null;
  });

  protected readonly name = computed(() => this.product()?.name ?? titleCase(this.params().slug));
  protected readonly categoryName = computed(
    () => CATEGORY_NAMES[this.params().category] ?? titleCase(this.params().category),
  );

  protected readonly crumbs = computed(() => [
    { label: this.categoryName(), link: `/${this.params().category}` },
    { label: this.name() },
  ]);

  protected readonly related = computed(() =>
    SAMPLE_PRODUCTS.filter((p) => p.slug !== this.params().slug).slice(0, 4),
  );

  protected readonly sizes = SAMPLE_SIZES;
  protected readonly selectedSize = signal('4–5 Y');

  protected readonly details = [
    {
      title: 'Details & fabric',
      text: '100% cotton poplin, pre-washed. Fully lined bodice.',
      open: true,
    },
    {
      title: 'Size & fit',
      text: 'Relaxed fit. Model is 5 years old and wears size 4–5 Y.',
      open: false,
    },
    {
      title: 'Delivery & returns',
      text: 'Delivered nationwide with tracking. Return unworn for an exchange.',
      open: false,
    },
    { title: 'Care', text: 'Machine wash cool. Line dry. Warm iron if needed.', open: false },
  ];

  protected selectSize(label: string): void {
    this.selectedSize.set(label);
  }
}
