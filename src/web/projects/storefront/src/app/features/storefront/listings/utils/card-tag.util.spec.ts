import { describe, expect, it } from 'vitest';
import { ListingTile } from '../models/listing.model';
import { cardTag } from './card-tag.util';

const base = { savingPercent: 0, hasLowStock: false, isNew: false } as Pick<ListingTile, 'savingPercent' | 'hasLowStock' | 'isNew'>;

describe('cardTag', () => {
  it('CRD-3 a discounted product shows the sale badge, whatever else is true', () => {
    expect(cardTag({ ...base, savingPercent: 25, hasLowStock: true, isNew: true })).toBe('sale');
  });

  it('CRD-3 without a discount, low stock wins over new', () => {
    expect(cardTag({ ...base, hasLowStock: true, isNew: true })).toBe('low');
  });

  it('CRD-3 without a discount or low stock, a new arrival says New', () => {
    expect(cardTag({ ...base, isNew: true })).toBe('new');
  });

  it('CRD-3 otherwise there is no tag, and a tile from an older response (missing fields) has none either', () => {
    expect(cardTag(base)).toBeNull();
    expect(cardTag({ savingPercent: 0 } as Pick<ListingTile, 'savingPercent' | 'hasLowStock' | 'isNew'>)).toBeNull();
  });
});
