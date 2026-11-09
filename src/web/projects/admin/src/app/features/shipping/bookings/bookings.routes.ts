import { Routes } from '@angular/router';
import { provideEffects } from '@ngrx/effects';
import { provideState } from '@ngrx/store';
import * as bookingsEffects from './store/bookings.effects';
import { bookingsReducer } from './store/bookings.reducer';
import { BOOKINGS_FEATURE_KEY } from './store/bookings.state';

export const BOOKINGS_ROUTES: Routes = [
  {
    path: '',
    providers: [provideState(BOOKINGS_FEATURE_KEY, bookingsReducer), provideEffects(bookingsEffects)],
    children: [
      {
        path: '',
        loadComponent: () => import('./containers/shipments-page/shipments-page.container').then((m) => m.ShipmentsPageContainer),
        title: 'Shipments · Romp Admin',
      },
      {
        path: ':id',
        loadComponent: () => import('./containers/shipment-detail-page/shipment-detail-page.container').then((m) => m.ShipmentDetailPageContainer),
        title: 'Shipment · Romp Admin',
      },
    ],
  },
];
