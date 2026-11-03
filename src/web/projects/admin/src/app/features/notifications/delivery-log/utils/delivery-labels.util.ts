import { Choice } from '../models';

/** The plain-words name for a lookup code; an unknown code is shown as it came, never hidden. */
export function nameOf(choices: readonly Choice[], code: string | null): string {
  if (code === null || code === '') return '';
  return choices.find((c) => c.code === code)?.name ?? code;
}

/** "3 min", "2 h 5 min": how long the oldest due message has waited. Zero means nothing is late. */
export function waitText(seconds: number): string {
  if (seconds <= 0) return 'Nothing waiting';
  if (seconds < 60) return `${seconds} s`;
  const minutes = Math.floor(seconds / 60);
  if (minutes < 60) return `${minutes} min`;
  const hours = Math.floor(minutes / 60);
  const rest = minutes % 60;
  return rest === 0 ? `${hours} h` : `${hours} h ${rest} min`;
}

/** One key per resend dialog, so a double click sends the same key twice and the server queues once (DLG-8). */
export function newResendKey(): string {
  return `rs-${crypto.randomUUID().replace(/-/g, '')}`;
}
