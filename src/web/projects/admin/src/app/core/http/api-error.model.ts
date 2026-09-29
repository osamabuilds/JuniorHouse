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
