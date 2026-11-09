import { EnvironmentProviders, PLATFORM_ID, TransferState, inject, makeEnvironmentProviders, provideEnvironmentInitializer } from '@angular/core';
import { isPlatformServer } from '@angular/common';
import { META_REDUCERS, Store } from '@ngrx/store';
import { take } from 'rxjs';
import { STATE_TRANSFER_KEY, SliceMap, createSeedingMetaReducer, serialiseSlices } from './state-transfer';

/**
 * ADR 0028: on the server, write the whitelisted result slices into the page; in the browser, seed the store from them once before the
 * first render, so hydration shows the same content the server sent instead of a loading state.
 */
export function provideStateTransfer(): EnvironmentProviders {
  return makeEnvironmentProviders([
    {
      provide: META_REDUCERS,
      multi: true,
      useFactory: () => {
        const transfer = inject(TransferState);
        const transferred = transfer.get(STATE_TRANSFER_KEY, null);
        // Used exactly once: the browser reads it when the store is created and the page no longer holds it.
        transfer.remove(STATE_TRANSFER_KEY);
        return createSeedingMetaReducer(transferred);
      },
    },
    provideEnvironmentInitializer(() => {
      if (!isPlatformServer(inject(PLATFORM_ID))) return;
      const transfer = inject(TransferState);
      const store = inject(Store);
      transfer.onSerialize(STATE_TRANSFER_KEY, () => {
        let snapshot: SliceMap = {};
        store.pipe(take(1)).subscribe((s) => (snapshot = s as SliceMap));
        return serialiseSlices(snapshot);
      });
    }),
  ]);
}
