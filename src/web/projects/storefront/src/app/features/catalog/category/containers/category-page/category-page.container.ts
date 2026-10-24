import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute } from '@angular/router';
import { map } from 'rxjs';
import { BreadcrumbsComponent } from '../../../../../shared/components/breadcrumbs/breadcrumbs.component';
import { ProductCardComponent } from '../../../../../shared/components/product-card/product-card.component';
import { CATEGORY_NAMES, SAMPLE_PRODUCTS, titleCase } from '../../../catalog.sample-data';

@Component({
  selector: 'app-category-page',
  imports: [BreadcrumbsComponent, ProductCardComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './category-page.container.html',
  styleUrl: './category-page.container.scss',
})
export class CategoryPageContainer {
  private readonly route = inject(ActivatedRoute);

  private readonly slug = toSignal(
    this.route.paramMap.pipe(map((params) => params.get('category') ?? '')),
    {
      initialValue: '',
    },
  );

  protected readonly name = computed(() => CATEGORY_NAMES[this.slug()] ?? titleCase(this.slug()));
  protected readonly crumbs = computed(() => [{ label: this.name() }]);

  protected readonly types = ['All', 'Dresses', 'Tops', 'Knitwear', 'Bottoms'];
  protected readonly activeType = signal('All');

  protected readonly products = computed(() => {
    const type = this.activeType().toLowerCase();
    return SAMPLE_PRODUCTS.filter((p) => type === 'all' || p.type === type);
  });

  protected selectType(type: string): void {
    this.activeType.set(type);
  }
}
