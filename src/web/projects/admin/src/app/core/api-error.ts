import { HttpErrorResponse } from '@angular/common/http';

/** Shape ASP.NET Core's ProblemDetails/ValidationProblemDetails send back (Romp.Api's exception handlers). */
interface ProblemDetailsBody {
  readonly title?: string;
  readonly detail?: string;
  readonly errors?: Record<string, string[]>;
}

/**
 * Normalises any failed request into one shape every screen can render the same way: a
 * human-readable message for a banner, and field-level errors (AC-15) to show inline next to the
 * form control they belong to.
 */
export interface ApiError {
  readonly status: number;
  readonly message: string;
  readonly fieldErrors: Readonly<Record<string, readonly string[]>>;
}

export function toApiError(error: HttpErrorResponse): ApiError {
  const body = isProblemDetailsBody(error.error) ? error.error : undefined;
  const specific = body?.detail ?? summariseFieldErrors(body?.errors);

  return {
    status: error.status,
    message: specific ?? plainMessageFor(error.status, body?.title),
    fieldErrors: body?.errors ?? {},
  };
}

/** A sentence a person can act on, for failures where the API gave no specific reason. */
function plainMessageFor(status: number, title: string | undefined): string {
  if (status === 0) {
    return "We couldn't reach the server. Check your internet connection and try again.";
  }
  if (status === 404) {
    return "We couldn't find that. It may have been removed.";
  }
  if (status >= 500) {
    return 'Something went wrong on our side. Please try again in a moment.';
  }
  return title ?? 'Something went wrong. Please try again.';
}

function isProblemDetailsBody(value: unknown): value is ProblemDetailsBody {
  return typeof value === 'object' && value !== null;
}

/** The actual rule violations, so a banner never says just "one or more errors occurred". */
function summariseFieldErrors(errors: Record<string, string[]> | undefined): string | undefined {
  const messages = Object.values(errors ?? {}).flat();
  return messages.length > 0 ? messages.join(' ') : undefined;
}
