import { createActionGroup, emptyProps, props } from '@ngrx/store';
import { ApiError } from '@core/http';
import { MfaEnrolment, MfaStatusDto } from '../models';

export const MfaPageActions = createActionGroup({
  source: 'Mfa Page',
  events: {
    Opened: emptyProps(),
    'Restart Clicked': emptyProps(),
    'Code Submitted': props<{ code: string }>(),
    'Codes Saved': emptyProps(),
    'Status Opened': emptyProps(),
    'Regenerate Submitted': props<{ code: string }>(),
  },
});

export const MfaApiActions = createActionGroup({
  source: 'Mfa API',
  events: {
    'Start Succeeded': props<{ enrolment: MfaEnrolment }>(),
    'Start Failed': props<{ error: ApiError }>(),
    'Confirm Succeeded': props<{ codes: readonly string[] }>(),
    'Confirm Failed': props<{ error: ApiError }>(),
    'Status Loaded': props<{ status: MfaStatusDto }>(),
    'Status Failed': props<{ error: ApiError }>(),
    'Regenerate Succeeded': props<{ codes: readonly string[] }>(),
    'Regenerate Failed': props<{ error: ApiError }>(),
  },
});
