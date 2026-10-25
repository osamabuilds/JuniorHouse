import { ChangeDetectionStrategy, Component, effect, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Store } from '@ngrx/store';
import { NavigationActions, selectHome } from '../../storefront/navigation';
import { ProductTileComponent } from '../../storefront/listings/components/product-tile/product-tile.component';
import { GarmentComponent } from '../../../shared/garment/garment.component';
import { SeoService } from '../../../shared/seo/seo.service';
import { selectBrand } from '../../storefront/site-settings';
import { IconComponent } from '../../../shared/ui/icon/icon.component';

/**
 * Home (design system HomePage). The audience tiles and New in come from the API (NAV-10); the hero, story band, gifts and fabric
 * blocks are still placeholder imagery and copy.
 */
@Component({
  selector: 'app-home-page',
  imports: [RouterLink, GarmentComponent, IconComponent, ProductTileComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './home-page.container.html',
})
export class HomePageContainer {
  private readonly store = inject(Store);
  protected readonly home = this.store.selectSignal(selectHome);
  private readonly seo = inject(SeoService);
  private readonly brand = this.store.selectSignal(selectBrand);
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

  constructor() {
    this.store.dispatch(NavigationActions.loadHome());
    // Home meta and the site-wide structured data come from the brand setting, so a renamed brand changes them all (NAV-10, NAV-11).
    effect(() => {
      const brand = this.brand();
      this.seo.set({
        title: brand.tagline || 'Home',
        description: brand.tagline ? `${brand.name}: ${brand.tagline}` : `Shop ${brand.name}.`,
        path: '/',
        siteName: brand.name,
        crumbs: [],
        structured: this.seo.siteData(brand.name),
      });
    });
  }
}
