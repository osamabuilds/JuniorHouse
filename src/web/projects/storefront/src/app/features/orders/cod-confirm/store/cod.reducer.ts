import { createFeature, createReducer, on } from '@ngrx/store';
import { CodLinkStatus, CodOutcome } from '../models/cod.model';
import { CodActions } from './cod.actions';

export interface CodState {
  readonly status: 'idle' | 'loading' | 'ready' | 'error';
  readonly link: CodLinkStatus | null;
  /** A press is on its way: the buttons wait so a double tap cannot answer twice. */
  readonly sending: boolean;
  readonly outcome: CodOutcome | null;
  readonly failed: boolean;
}

export const initialCodState: CodState = { status: 'idle', link: null, sending: false, outcome: null, failed: false };

export const codFeature = createFeature({
  name: 'codConfirm',
  reducer: createReducer(
    initialCodState,
    on(CodActions.statusRequested, (): CodState => ({ ...initialCodState, status: 'loading' })),
    on(CodActions.statusLoaded, (s, { result }): CodState => (result.status === 'ok' ? { ...s, status: 'ready', link: result.link } : { ...s, status: 'error' })),
    on(CodActions.answered, (s): CodState => ({ ...s, sending: true, failed: false })),
    on(CodActions.answerDone, (s, { result }): CodState =>
      result.status === 'ok' ? { ...s, sending: false, outcome: result.outcome } : { ...s, sending: false, failed: true },
    ),
  ),
});

export const { selectStatus: selectCodStatus, selectLink: selectCodLink, selectSending: selectCodSending, selectOutcome: selectCodOutcome, selectFailed: selectCodFailed } = codFeature;
