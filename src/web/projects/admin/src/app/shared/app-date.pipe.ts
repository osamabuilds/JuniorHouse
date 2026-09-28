import { Pipe, PipeTransform } from '@angular/core';

const DATE = new Intl.DateTimeFormat('en-GB', { day: 'numeric', month: 'short', year: 'numeric' });
const DATE_TIME = new Intl.DateTimeFormat('en-GB', {
  day: 'numeric',
  month: 'short',
  year: 'numeric',
  hour: 'numeric',
  minute: '2-digit',
  hour12: true,
});

/**
 * Shows the API's ISO dates the way people read them: "1 Dec 2026", or "28 Sep 2026, 2:33 pm"
 * with `datetime`. A date-only value (2026-12-01) is read as that calendar day, never shifted by
 * the time zone. Empty stays an em dash; anything unreadable is shown as it came.
 */
export function formatDate(value: string | null | undefined, kind: 'date' | 'datetime' = 'date'): string {
  if (!value) {
    return '—';
  }

  const dateOnly = /^(\d{4})-(\d{2})-(\d{2})$/.exec(value);
  const parsed = dateOnly ? new Date(+dateOnly[1], +dateOnly[2] - 1, +dateOnly[3]) : new Date(value);
  if (Number.isNaN(parsed.getTime())) {
    return value;
  }

  return kind === 'datetime' && !dateOnly ? DATE_TIME.format(parsed) : DATE.format(parsed);
}

@Pipe({ name: 'appDate' })
export class AppDatePipe implements PipeTransform {
  transform(value: string | null | undefined, kind: 'date' | 'datetime' = 'date'): string {
    return formatDate(value, kind);
  }
}
