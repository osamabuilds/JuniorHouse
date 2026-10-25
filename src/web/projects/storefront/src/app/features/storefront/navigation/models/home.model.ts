import { ListingTile } from '../../listings/models/listing.model';

export interface HomeAudience {
  readonly code: string;
  readonly name: string;
  readonly path: string;
}

/** GET /api/storefront/home: up to twelve New Arrivals (or the newest listable when nothing is new) and an audience tile each (NAV-10). */
export interface Home {
  readonly newArrivals: readonly ListingTile[];
  readonly isFallback: boolean;
  readonly audiences: readonly HomeAudience[];
}
