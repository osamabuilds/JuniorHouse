import { Injectable, inject } from '@angular/core';
import { Actions, createEffect, ofType } from '@ngrx/effects';
import { Store } from '@ngrx/store';
import { concatMap, filter, map, switchMap, withLatestFrom } from 'rxjs';
import { PAGE_SIZE } from '../models/address.model';
import { AddressesApi } from '../services/addresses-api.service';
import { AddressesActions } from './addresses.actions';
import { addressesFeature } from './addresses.reducer';

@Injectable()
export class AddressesEffects {
  private readonly actions$ = inject(Actions);
  private readonly api = inject(AddressesApi);
  private readonly store = inject(Store);

  /** switchMap: asking for another page cancels the request for the one before. */
  readonly load$ = createEffect(() =>
    this.actions$.pipe(
      ofType(AddressesActions.load),
      switchMap(({ page }) => this.api.list(page, PAGE_SIZE).pipe(map((result) => AddressesActions.loadDone({ result })))),
    ),
  );

  /** A page left empty by a removal (not the first) steps back one page. */
  readonly stepBack$ = createEffect(() =>
    this.actions$.pipe(
      ofType(AddressesActions.loadDone),
      filter(({ result }) => result.status === 'ok' && result.page.items.length === 0 && result.page.page > 1),
      map(({ result }) => AddressesActions.load({ page: (result.status === 'ok' ? result.page.page : 2) - 1 })),
    ),
  );

  /** concatMap: changes run in the order the customer made them. */
  readonly save$ = createEffect(() =>
    this.actions$.pipe(
      ofType(AddressesActions.save),
      concatMap(({ id, input }) =>
        (id === null ? this.api.create(input) : this.api.update(id, input)).pipe(map((result) => AddressesActions.changeDone({ kind: 'save', result }))),
      ),
    ),
  );

  readonly remove$ = createEffect(() =>
    this.actions$.pipe(
      ofType(AddressesActions.remove),
      concatMap(({ id }) => this.api.remove(id).pipe(map((result) => AddressesActions.changeDone({ kind: 'remove', result })))),
    ),
  );

  readonly makeDefault$ = createEffect(() =>
    this.actions$.pipe(
      ofType(AddressesActions.makeDefault),
      concatMap(({ id }) => this.api.makeDefault(id).pipe(map((result) => AddressesActions.changeDone({ kind: 'default', result })))),
    ),
  );

  /** After a change that went through (or found the address gone, or hit a rule) the list is read again, so it shows what the server holds. */
  readonly reload$ = createEffect(() =>
    this.actions$.pipe(
      ofType(AddressesActions.changeDone),
      filter(({ result }) => result.status === 'ok' || result.status === 'notFound' || result.status === 'refused'),
      withLatestFrom(this.store.select(addressesFeature.selectPageNo)),
      map(([, pageNo]) => AddressesActions.load({ page: pageNo })),
    ),
  );

  readonly provinces$ = createEffect(() =>
    this.actions$.pipe(
      ofType(AddressesActions.loadProvinces),
      switchMap(() => this.api.provinces().pipe(map((provinces) => AddressesActions.loadProvincesDone({ provinces })))),
    ),
  );

  /** One request per province, ever: a province already loaded is not asked for again. */
  readonly cities$ = createEffect(() =>
    this.actions$.pipe(
      ofType(AddressesActions.loadCities),
      withLatestFrom(this.store.select(addressesFeature.selectCities)),
      filter(([{ provinceId }, cities]) => provinceId > 0 && !cities[provinceId]),
      concatMap(([{ provinceId }]) => this.api.cities(provinceId).pipe(map((cities) => AddressesActions.loadCitiesDone({ provinceId, cities })))),
    ),
  );
}
