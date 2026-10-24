import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { CATALOG, CATEGORY_NAMES, SIZES, titleCase } from '../../../shared/data/mock-catalog';
import { GarmentComponent } from '../../../shared/garment/garment.component';
import { ProductCardComponent } from '../../../shared/product-card/product-card.component';
import { IconComponent } from '../../../shared/ui/icon/icon.component';

const COLOURS = [
  { name: 'Oat', token: 'oat' },
  { name: 'Sage', token: 'sage' },
  { name: 'Clay', token: 'clay' },
] as const;

/** Product page (design system ProductPage): gallery, buy panel with size and colour, details, fabric band, rail. Sample data. */
@Component({
  selector: 'app-product-page',
  imports: [DecimalPipe, RouterLink, GarmentComponent, IconComponent, ProductCardComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './product-page.container.html',
})
export class ProductPageContainer {
  private readonly params = toSignal(inject(ActivatedRoute).paramMap, { requireSync: true });

  protected readonly product = computed(() => CATALOG.find((p) => p.slug === this.params().get('slug')) ?? null);
  protected readonly name = computed(() => this.product()?.name ?? titleCase(this.params().get('slug') ?? ''));
  protected readonly audience = computed(() => this.params().get('category') ?? '');
  protected readonly audienceName = computed(() => CATEGORY_NAMES[this.audience()] ?? titleCase(this.audience()));
  protected readonly typeName = computed(() => titleCase(this.params().get('type') ?? ''));
  protected readonly related = computed(() => CATALOG.filter((p) => p.slug !== this.product()?.slug).slice(0, 6));

  protected readonly colours = COLOURS;
  protected readonly sizes = SIZES;
  protected readonly colour = signal<string>('Oat');
  protected readonly size = signal<string>('4–5Y');
  protected readonly wished = signal(false);
  protected readonly views = [0, 1, 2, 3, 4, 5];
  protected readonly tones = ['oat', 'stone', 'sand', 'clay', 'sage', 'oat'] as const;

  protected readonly fabrics = [
    { name: 'Washed linen', note: '100% linen, softens with every wash', tex: 'linen' },
    { name: 'Cotton rib', note: 'Stretchy and gentle on skin', tex: 'rib' },
    { name: 'Cotton twill', note: 'Sturdy enough for playground days', tex: 'twill' },
  ] as const;

  protected pickSize(label: string, inStock: boolean): void {
    if (inStock) {
      this.size.set(label);
    }
  }
}
