import { createFeature, createReducer, on } from '@ngrx/store';
import { NotifyForm } from '../models/notify-me.model';
import { NotifyMeActions } from './notify-me.actions';

export interface NotifyMeState {
  /** The form state per size (variant id). A size that was never touched has no entry. */
  readonly forms: Readonly<Record<number, NotifyForm>>;
}

export const initialNotifyMeState: NotifyMeState = { forms: {} };

export const notifyMeFeature = createFeature({
  name: 'notifyMe',
  reducer: createReducer(
    initialNotifyMeState,
    on(NotifyMeActions.request, (s, { variantId }): NotifyMeState => ({ forms: { ...s.forms, [variantId]: { status: 'sending' } } })),
    on(
      NotifyMeActions.requestDone,
      (s, { variantId, result }): NotifyMeState => ({ forms: { ...s.forms, [variantId]: result.status === 'ok' ? { status: 'done' } : result } }),
    ),
    on(NotifyMeActions.reset, (s, { variantId }): NotifyMeState => {
      const forms = { ...s.forms };
      delete forms[variantId];
      return { forms };
    }),
  ),
});
