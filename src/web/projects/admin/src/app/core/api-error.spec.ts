import { HttpErrorResponse } from '@angular/common/http';
import { toApiError } from './api-error';

describe('toApiError', () => {
  it('shows the API detail when there is one', () => {
    const error = toApiError(
      new HttpErrorResponse({
        status: 400,
        error: { title: 'The request has invalid details.', detail: 'A channel is required.', errors: { ChannelId: ['A channel is required.'] } },
      }),
    );

    expect(error.message).toBe('A channel is required.');
    expect(error.fieldErrors['ChannelId']).toEqual(['A channel is required.']);
  });

  it('spells out the actual violations, never a generic "one or more errors" line (SCRUM-93)', () => {
    const error = toApiError(
      new HttpErrorResponse({
        status: 400,
        error: {
          title: 'One or more validation errors occurred.',
          errors: { ChannelId: ['A channel is required.'], ResponderName: ["'Responder Name' must not be empty."] },
        },
      }),
    );

    expect(error.message).toBe("A channel is required. 'Responder Name' must not be empty.");
    expect(error.message).not.toContain('One or more');
  });

  it('surfaces a style-guard rejection that names the blocking PO numbers (task 55)', () => {
    const error = toApiError(
      new HttpErrorResponse({
        status: 400,
        error: { errors: { SizeIds: ['Size/colour still used by PO(s) PO-2026-00007, PO-2026-00009 cannot be removed.'] } },
      }),
    );

    expect(error.message).toContain('PO-2026-00007');
    expect(error.fieldErrors['SizeIds']?.[0]).toContain('PO-2026-00009');
  });

  it('falls back to a plain message when the body is not problem details', () => {
    const error = toApiError(new HttpErrorResponse({ status: 500, error: null }));

    expect(error.message.length).toBeGreaterThan(0);
  });
});
