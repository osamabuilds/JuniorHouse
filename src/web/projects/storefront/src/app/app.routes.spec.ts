import { UrlSegment } from '@angular/router';
import { productMatcher, routes } from './app.routes';

const segments = (...paths: string[]) => paths.map((p) => new UrlSegment(p, {}));

describe('routes', () => {
  it('PDP-13 a three-part address goes to the product page, whatever the parts are', () => {
    expect(productMatcher(segments('girls', 'dresses', 'floral'), null as never, null as never)).toEqual({ consumed: segments('girls', 'dresses', 'floral') });
    expect(productMatcher(segments('a', 'b', 'c'), null as never, null as never)).not.toBeNull();
  });

  it('SLD-10 a season category address is its own route, listed before the product matcher so it is not taken for a product', () => {
    const paths = routes.map((r) => r.path);
    const own = paths.indexOf('collections/:code/:category');
    expect(own).toBeGreaterThan(-1);
    expect(routes[own].data).toEqual({ kind: 'collection' });
    expect(own).toBeLessThan(routes.findIndex((r) => r.matcher === productMatcher));
  });

  it('PDP-13 one, two and four parts are never a product', () => {
    for (const parts of [['girls'], ['girls', 'dresses'], ['collections', 'summer'], ['a', 'b', 'c', 'd']]) {
      expect(productMatcher(segments(...parts), null as never, null as never)).toBeNull();
    }
  });
});
