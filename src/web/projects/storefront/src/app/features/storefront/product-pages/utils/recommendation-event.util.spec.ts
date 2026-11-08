import { describe, expect, it, vi } from 'vitest';
import { buildEventBody, sendRecommendationEvent } from './recommendation-event.util';

describe('recommendation event', () => {
  it('REC-12 the body holds only the row, the product and the action', () => {
    expect(buildEventBody('TRENDING', 7, 'add')).toEqual({ row: 'TRENDING', productId: 7, action: 'add' });
    expect(Object.keys(buildEventBody('SIMILAR', 1, 'click')).sort()).toEqual(['action', 'productId', 'row']);
  });

  it('REC-12 uses sendBeacon when it takes the message, and then no fetch', async () => {
    const sendBeacon = vi.fn().mockReturnValue(true);
    const fetch = vi.fn();

    expect(sendRecommendationEvent(buildEventBody('SIMILAR', 3, 'click'), { sendBeacon, fetch })).toBe(true);

    const [url, blob] = sendBeacon.mock.calls[0] as [string, Blob];
    expect(url).toBe('/api/storefront/products/recommendations/events');
    expect(JSON.parse(await blob.text())).toEqual({ row: 'SIMILAR', productId: 3, action: 'click' });
    expect(fetch).not.toHaveBeenCalled();
  });

  it('REC-12 falls back to a keepalive fetch when sendBeacon is missing or refuses', () => {
    const fetch = vi.fn().mockResolvedValue({});
    const body = buildEventBody('SIMILAR', 3, 'add');

    expect(sendRecommendationEvent(body, { fetch })).toBe(true);
    expect(sendRecommendationEvent(body, { sendBeacon: () => false, fetch })).toBe(true);

    expect(fetch).toHaveBeenCalledTimes(2);
    expect(fetch.mock.calls[0][1]).toMatchObject({ method: 'POST', keepalive: true, body: JSON.stringify(body) });
  });

  it('a transport that throws never reaches the page', () => {
    const body = buildEventBody('SIMILAR', 3, 'click');
    expect(
      sendRecommendationEvent(body, {
        sendBeacon: () => {
          throw new Error('no');
        },
      }),
    ).toBe(false);
    expect(sendRecommendationEvent(body, {})).toBe(false);
  });
});
