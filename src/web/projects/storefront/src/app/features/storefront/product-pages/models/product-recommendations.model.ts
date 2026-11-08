import { ListingTile } from '../../listings/models/listing.model';

/** The rows a recommendation card can come from (REC-12). The codes are the API's. */
export type RecommendationRow = 'SIMILAR' | 'TRENDING';

export type RecommendationAction = 'click' | 'add';

/** GET /api/storefront/products/recommendations: the two rows under a product. Either may be empty; an empty row is not shown (REC-10). */
export interface ProductRecommendations {
  readonly similar: readonly ListingTile[];
  readonly trending: readonly ListingTile[];
}

export const NO_RECOMMENDATIONS: ProductRecommendations = { similar: [], trending: [] };
