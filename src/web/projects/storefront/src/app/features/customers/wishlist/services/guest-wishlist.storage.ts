import { isPlatformBrowser } from '@angular/common';
import { Injectable, PLATFORM_ID, inject } from '@angular/core';
import { MAX_GUEST_IDS } from '../models/wishlist.model';

export const GUEST_KEY = 'romp.wishlist.guest';

/**
 * What a signed-out shopper tapped, kept in this browser only: at most 50 product ids and nothing else, no personal data (WSL-2). On
 * sign-in the ids go to the merge endpoint once and this copy is cleared (WSL-3). Storage can be missing or blocked (private window,
 * server render), so every call is wrapped and the page works without it.
 */
@Injectable({ providedIn: 'root' })
export class GuestWishlistStorage {
  private readonly browser = isPlatformBrowser(inject(PLATFORM_ID));

  read(): number[] {
    if (!this.browser) return [];
    try {
      const parsed: unknown = JSON.parse(localStorage.getItem(GUEST_KEY) ?? '[]');
      if (!Array.isArray(parsed)) return [];
      return [...new Set(parsed.filter((v): v is number => Number.isInteger(v) && v > 0))].slice(0, MAX_GUEST_IDS);
    } catch {
      return [];
    }
  }

  write(ids: readonly number[]): void {
    if (!this.browser) return;
    try {
      if (ids.length === 0) localStorage.removeItem(GUEST_KEY);
      else localStorage.setItem(GUEST_KEY, JSON.stringify(ids.slice(0, MAX_GUEST_IDS)));
    } catch {
      // Blocked storage: the heart still works for this visit, it just is not remembered.
    }
  }

  clear(): void {
    this.write([]);
  }
}
