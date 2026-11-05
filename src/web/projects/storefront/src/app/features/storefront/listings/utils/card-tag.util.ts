import { ListingTile } from '../models/listing.model';

export type CardTag = 'sale' | 'low' | 'new';

/**
 * The one small tag a card may carry (design system: ProductCard; CRD-3): the sale badge when discounted, else "Low stock", else "New".
 * Never more than one, never a Bestseller badge. A response from before these facts existed simply has no tag.
 */
export function cardTag(tile: Pick<ListingTile, 'savingPercent' | 'hasLowStock' | 'isNew'>): CardTag | null {
  if ((tile.savingPercent ?? 0) > 0) return 'sale';
  if (tile.hasLowStock) return 'low';
  if (tile.isNew) return 'new';
  return null;
}
