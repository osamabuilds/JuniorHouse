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

  return {
    status: error.status,
    message: body?.detail ?? summariseFieldErrors(body?.errors) ?? body?.title ?? error.message ?? 'Something went wrong. Please try again.',
    fieldErrors: body?.errors ?? {},
  };
}

function isProblemDetailsBody(value: unknown): value is ProblemDetailsBody {
  return typeof value === 'object' && value !== null;
}

/** The actual rule violations, so a banner never says just "one or more errors occurred". */
function summariseFieldErrors(errors: Record<string, string[]> | undefined): string | undefined {
  const messages = Object.values(errors ?? {}).flat();
  return messages.length > 0 ? messages.join(' ') : undefined;
}
