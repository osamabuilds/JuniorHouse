import { TestBed } from '@angular/core/testing';
import { GUEST_KEY, GuestWishlistStorage } from './guest-wishlist.storage';

describe('GuestWishlistStorage', () => {
  let storage: GuestWishlistStorage;

  beforeEach(() => {
    localStorage.clear();
    storage = TestBed.inject(GuestWishlistStorage);
  });
  afterEach(() => localStorage.clear());

  it('WSL-2 remembers product ids in this browser and nothing else', () => {
    storage.write([3, 2, 1]);

    expect(JSON.parse(localStorage.getItem(GUEST_KEY) ?? 'null')).toEqual([3, 2, 1]);
    expect(storage.read()).toEqual([3, 2, 1]);
  });

  it('WSL-2 keeps at most 50 ids', () => {
    storage.write(Array.from({ length: 80 }, (_, i) => i + 1));

    expect(storage.read()).toHaveLength(50);
  });

  it('WSL-3 clear removes the key once the ids were merged', () => {
    storage.write([1]);
    storage.clear();

    expect(localStorage.getItem(GUEST_KEY)).toBeNull();
  });

  it('ignores a damaged value instead of failing', () => {
    localStorage.setItem(GUEST_KEY, '{"not":"a list"');
    expect(storage.read()).toEqual([]);

    localStorage.setItem(GUEST_KEY, JSON.stringify([1, 'x', -4, 1.5, 7, 7]));
    expect(storage.read()).toEqual([1, 7]);
  });
});
