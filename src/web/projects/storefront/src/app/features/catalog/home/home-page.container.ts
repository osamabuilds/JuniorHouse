import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { CATALOG } from '../../../shared/data/mock-catalog';
import { GarmentComponent } from '../../../shared/garment/garment.component';
import { ProductCardComponent } from '../../../shared/product-card/product-card.component';
import { IconComponent } from '../../../shared/ui/icon/icon.component';

/** Home (design system HomePage): hero, category split, new in, story band, gifts, fabric and trust line. Placeholder imagery and copy. */
@Component({
  selector: 'app-home-page',
  imports: [RouterLink, GarmentComponent, IconComponent, ProductCardComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './home-page.container.html',
})
export class HomePageContainer {
  protected readonly newIn = CATALOG.slice(0, 8);
  protected readonly gifts = [
    { title: 'Under Rs. 2,000', note: 'Small gestures', tone: 'oat', shape: 'hat' },
    { title: 'Newborn sets', note: 'Soft first things', tone: 'sage', shape: 'shirt' },
    { title: 'Eid and occasion', note: 'Dressed to celebrate', tone: 'clay', shape: 'dress' },
    { title: 'Gift cards', note: 'Let them choose', tone: 'stone', shape: 'trouser' },
  ] as const;
  protected readonly fabrics = [
    { name: 'Washed linen', note: '100% linen, softens with every wash', tex: 'linen' },
    { name: 'Cotton rib', note: 'Stretchy and gentle on skin', tex: 'rib' },
    { name: 'Cotton twill', note: 'Sturdy enough for playground days', tex: 'twill' },
  ] as const;
}
