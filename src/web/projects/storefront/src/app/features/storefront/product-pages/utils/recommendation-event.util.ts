import { RecommendationAction, RecommendationRow } from '../models/product-recommendations.model';

const URL = '/api/storefront/products/recommendations/events';

/** What a recommendation event says: the row, the product and what happened. Nothing about the visitor (REC-12). */
export interface RecommendationEventBody {
  readonly row: RecommendationRow;
  readonly productId: number;
  readonly action: RecommendationAction;
}

export function buildEventBody(row: RecommendationRow, productId: number, action: RecommendationAction): RecommendationEventBody {
  return { row, productId, action };
}

/** The browser features the sender uses, so a test can pass fakes. */
export interface EventTransport {
  readonly sendBeacon?: (url: string, data: Blob) => boolean;
  readonly fetch?: (url: string, init: { method: string; headers: Record<string, string>; body: string; keepalive: boolean }) => Promise<unknown>;
}

/**
 * Sends the event without waiting and without ever failing the page: `sendBeacon` when it takes the message (it survives the page being left
 * by the click), else `fetch` with `keepalive`. Call it from the browser only; the server render must send nothing.
 */
export function sendRecommendationEvent(body: RecommendationEventBody, transport: EventTransport): boolean {
  const json = JSON.stringify(body);
  try {
    if (transport.sendBeacon?.(URL, new Blob([json], { type: 'application/json' }))) return true;
    if (transport.fetch) {
      void transport.fetch(URL, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: json, keepalive: true }).catch(() => undefined);
      return true;
    }
  } catch {
    // Counting must never disturb the shopper.
  }
  return false;
}
