import { Component, computed, inject } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { Store } from '@ngrx/store';
import { catchError, filter, map, of, switchMap, timer } from 'rxjs';
import { HasPermissionDirective, SessionPageActions, selectSessionUser } from '@core/auth';
import { OutboxHealthService } from '@core/health';
import { AlertBadgeComponent } from '@features/warehouse/stock-alerts';
import { OutboxHealthComponent } from '@shared';

const VENDOR_VIEW_SEGMENT = '/vendor-view';
/** Pages shown on their own, without the staff header, nav and footer: the vendor-facing PO view and the pre-sign-in pages. */
const CHROMELESS_SEGMENTS = [VENDOR_VIEW_SEGMENT, '/sign-in', '/change-password', '/setup-required'];

const isChromeless = (url: string): boolean => CHROMELESS_SEGMENTS.some((segment) => url.includes(segment));

/**
 * The admin shell (SCRUM-174): semantic landmarks (header/nav/main/footer), one per page, with
 * the sections in the nav. Every routed screen renders inside <main>. The vendor-facing PO view
 * (SCRUM-93 task 51) and the sign-in pages are shown without the admin header, nav and footer - a vendor is never shown
 * staff navigation, and a visitor who is not signed in is never shown any. Asks who the caller is when the app starts (sessions AC-9).
 */
@Component({
  selector: 'app-root',
  imports: [RouterLink, RouterLinkActive, RouterOutlet, OutboxHealthComponent, AlertBadgeComponent, HasPermissionDirective],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App {
  private readonly router = inject(Router);
  private readonly store = inject(Store);
  private readonly outboxHealthService = inject(OutboxHealthService);

  /** Null until /me answers, and again after sign-out: the staff chrome and its polling exist only for a signed-in person. */
  readonly user = this.store.selectSignal(selectSessionUser);
  /** The development stand-in user (no session behind it) has nothing to sign out of. */
  readonly canSignOut = computed(() => (this.user()?.userId ?? 0) > 0);

  /** SCRUM-186: polled every 30 seconds while a fully signed-in person is looking; when the API cannot answer, nothing is shown rather than a false alarm. */
  readonly outboxHealth = toSignal(
    toObservable(computed(() => this.user()?.scope === 'Full')).pipe(
      switchMap((signedIn) =>
        signedIn
          ? timer(0, 30_000).pipe(switchMap(() => this.outboxHealthService.read().pipe(catchError(() => of(null)))))
          : of(null),
      ),
    ),
    { initialValue: null },
  );

  readonly chromeless = toSignal(
    this.router.events.pipe(
      filter((event) => event instanceof NavigationEnd),
      map((event) => isChromeless(event.urlAfterRedirects)),
    ),
    { initialValue: isChromeless(this.router.url) },
  );

  constructor() {
    this.store.dispatch(SessionPageActions.started());
  }

  signOut(): void {
    this.store.dispatch(SessionPageActions.signOutClicked());
  }

  signOutEverywhere(): void {
    this.store.dispatch(SessionPageActions.signOutEverywhereClicked());
  }
}
