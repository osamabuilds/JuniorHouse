import { createFeature, createReducer, on } from '@ngrx/store';
import { NewsletterActions } from './newsletter.actions';

export type FooterStatus = 'idle' | 'sending' | 'done' | 'invalid' | 'busy' | 'error';
export type MineStatus = 'idle' | 'loading' | 'ready' | 'signedOut' | 'error';

export interface NewsletterState {
  readonly footer: FooterStatus;
  readonly mine: MineStatus;
  readonly subscribed: boolean;
  /** A change on the preferences page is on its way. */
  readonly saving: boolean;
  /** The last change on the preferences page failed. */
  readonly saveFailed: boolean;
}

export const initialNewsletterState: NewsletterState = { footer: 'idle', mine: 'idle', subscribed: false, saving: false, saveFailed: false };

export const newsletterFeature = createFeature({
  name: 'newsletter',
  reducer: createReducer(
    initialNewsletterState,
    on(NewsletterActions.subscribe, (s): NewsletterState => ({ ...s, footer: 'sending' })),
    on(NewsletterActions.subscribeDone, (s, { result }): NewsletterState => ({ ...s, footer: result === 'ok' ? 'done' : result })),
    on(NewsletterActions.resetFooter, (s): NewsletterState => ({ ...s, footer: 'idle' })),
    on(NewsletterActions.loadMine, (s): NewsletterState => ({ ...s, mine: 'loading' })),
    on(NewsletterActions.loadMineDone, (s, { result }): NewsletterState => {
      switch (result.status) {
        case 'ok':
          return { ...s, mine: 'ready', subscribed: result.subscribed };
        case 'signedOut':
          return { ...s, mine: 'signedOut' };
        default:
          return { ...s, mine: 'error' };
      }
    }),
    on(NewsletterActions.setMine, (s): NewsletterState => ({ ...s, saving: true, saveFailed: false })),
    on(NewsletterActions.setMineDone, (s, { result }): NewsletterState =>
      result.status === 'ok' ? { ...s, saving: false, subscribed: result.subscribed } : { ...s, saving: false, saveFailed: true },
    ),
  ),
});

export const {
  selectFooter: selectNewsletterFooter,
  selectMine: selectNewsletterMine,
  selectSubscribed: selectNewsletterSubscribed,
  selectSaving: selectNewsletterSaving,
  selectSaveFailed: selectNewsletterSaveFailed,
} = newsletterFeature;
