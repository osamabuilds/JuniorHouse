import { Injectable, signal } from '@angular/core';

const STORAGE_KEY = 'romp.admin.actingPerson';

/**
 * Until staff sign-in exists (Sprint 8), every recorded action carries the name of the person acting, typed
 * on the form (Sprint 3, D13). This remembers the last name typed in this browser so it is a one-click
 * default next time. It is a convenience only: the form still requires the name, and the API stores it
 * next to the system actor. Reading or writing browser storage can fail (private windows), so both are guarded.
 */
@Injectable({ providedIn: 'root' })
export class ActingPersonService {
  private readonly current = signal(read());

  /** The last name used in this browser, or an empty string. */
  readonly name = this.current.asReadonly();

  remember(name: string): void {
    const trimmed = name.trim();
    if (!trimmed) {
      return;
    }

    this.current.set(trimmed);
    try {
      localStorage.setItem(STORAGE_KEY, trimmed);
    } catch {
      // Storage unavailable: the name is still remembered for this session.
    }
  }
}

function read(): string {
  try {
    return localStorage.getItem(STORAGE_KEY) ?? '';
  } catch {
    return '';
  }
}
