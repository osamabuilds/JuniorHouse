import { TestBed } from '@angular/core/testing';
import { provideEffects } from '@ngrx/effects';
import { Store, provideStore } from '@ngrx/store';
import { Observable, Subject, of } from 'rxjs';
import { provideCodConfirm } from '../cod-confirm.providers';
import { CodAnswer, CodRespondResult, CodStatusResult } from '../models/cod.model';
import { CodApi } from '../services/cod-api.service';
import { CodActions } from './cod.actions';
import { selectCodLink, selectCodOutcome, selectCodSending, selectCodStatus } from './cod.reducer';

class FakeApi {
  status: (t: string) => Observable<CodStatusResult> = () => of({ status: 'ok', link: { isValid: true, orderNumber: 'RMP-2026-00001-4', canRespond: true, statusCode: 'PNDC' } });
  respond: (t: string, a: CodAnswer) => Observable<CodRespondResult> = () => of({ status: 'ok', outcome: 'Confirmed', orderNumber: 'RMP-2026-00001-4' });
}

function setUp(api = new FakeApi()) {
  TestBed.configureTestingModule({ providers: [provideStore(), provideEffects(), provideCodConfirm(), { provide: CodApi, useValue: api }] });
  return { store: TestBed.inject(Store) };
}

describe('cod confirm store', () => {
  it('COD-12 opening the link reads its status and answers nothing', () => {
    let answered = 0;
    const api = new FakeApi();
    api.respond = () => {
      answered++;
      return of({ status: 'ok', outcome: 'Confirmed', orderNumber: null });
    };
    const { store } = setUp(api);

    store.dispatch(CodActions.statusRequested({ token: 't' }));

    expect(store.selectSignal(selectCodStatus)()).toBe('ready');
    expect(store.selectSignal(selectCodLink)()?.canRespond).toBe(true);
    expect(answered).toBe(0);
  });

  it('COD-12 a press records the server outcome', () => {
    const { store } = setUp();

    store.dispatch(CodActions.answered({ token: 't', answer: 'confirm' }));

    expect(store.selectSignal(selectCodOutcome)()).toBe('Confirmed');
    expect(store.selectSignal(selectCodSending)()).toBe(false);
  });

  it('a second press while one is on its way is ignored', () => {
    const wait = new Subject<CodRespondResult>();
    let sent = 0;
    const api = new FakeApi();
    api.respond = () => {
      sent++;
      return wait;
    };
    const { store } = setUp(api);

    store.dispatch(CodActions.answered({ token: 't', answer: 'confirm' }));
    store.dispatch(CodActions.answered({ token: 't', answer: 'cancel' }));

    expect(sent).toBe(1);
    expect(store.selectSignal(selectCodSending)()).toBe(true);
  });
});
