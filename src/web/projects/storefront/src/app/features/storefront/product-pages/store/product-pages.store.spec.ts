import { TestBed } from '@angular/core/testing';
import { provideEffects } from '@ngrx/effects';
import { Store, provideStore } from '@ngrx/store';
import { Subject } from 'rxjs';
import { ProductPage, ProductPageResult } from '../models/product-page.model';
import { ProductPagesApi } from '../services/product-pages-api.service';
import { ProductPagesActions } from './product-pages.actions';
import { ProductPagesEffects } from './product-pages.effects';
import { productPagesFeature } from './product-pages.reducer';
import { selectProductPageKey, selectProductPageResult } from './product-pages.selectors';

const found = (name: string): ProductPageResult => ({ status: 'found', page: { name } as ProductPage });

function setup() {
  const calls: { path: string; colour: string | null; subject: Subject<ProductPageResult> }[] = [];
  TestBed.configureTestingModule({
    providers: [
      provideStore({ [productPagesFeature.name]: productPagesFeature.reducer }),
      provideEffects(ProductPagesEffects),
      {
        provide: ProductPagesApi,
        useValue: {
          get: (path: string, selection: { colour: string | null }) => {
            const subject = new Subject<ProductPageResult>();
            calls.push({ path, colour: selection.colour, subject });
            return subject;
          },
        },
      },
    ],
  });
  const store = TestBed.inject(Store);
  return { store, calls, result: () => store.selectSignal(selectProductPageResult)(), key: () => store.selectSignal(selectProductPageKey)() };
}

describe('product pages store', () => {
  it('PDP-1 keeps the result of the request that is current', () => {
    const { store, calls, result, key } = setup();

    store.dispatch(ProductPagesActions.load({ key: 'a', path: '/p', selection: { colour: null, size: null } }));
    calls[0].subject.next(found('A'));

    expect(key()).toBe('a');
    expect(result()).toEqual(found('A'));
  });

  it('PDP-2 a newer choice cancels the older request, and a late answer for it is ignored', () => {
    const { store, calls, result } = setup();

    store.dispatch(ProductPagesActions.load({ key: 'red', path: '/p', selection: { colour: 'red', size: null } }));
    store.dispatch(ProductPagesActions.load({ key: 'navy', path: '/p', selection: { colour: 'navy', size: null } }));
    calls[0].subject.next(found('red page'));
    calls[1].subject.next(found('navy page'));

    expect(result()).toEqual(found('navy page'));
  });

  it('keeps the previous page on screen while the next choice loads', () => {
    const { store, calls, result } = setup();
    store.dispatch(ProductPagesActions.load({ key: 'red', path: '/p', selection: { colour: 'red', size: null } }));
    calls[0].subject.next(found('red page'));

    store.dispatch(ProductPagesActions.load({ key: 'navy', path: '/p', selection: { colour: 'navy', size: null } }));

    expect(result()).toEqual(found('red page'));
  });
});
