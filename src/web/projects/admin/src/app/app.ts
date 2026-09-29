import { Component, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { catchError, filter, map, of, switchMap, timer } from 'rxjs';
import { OutboxHealthService } from '@core/health';
import { OutboxHealthComponent } from '@shared';

const VENDOR_VIEW_SEGMENT = '/vendor-view';

/**
 * The admin shell (SCRUM-174): semantic landmarks (header/nav/main/footer), one per page, with
 * the sections in the nav. Every routed screen renders inside <main>. The vendor-facing PO view
 * (SCRUM-93 task 51) is shown without the admin header, nav and footer - a vendor is never shown
 * staff navigation.
 */
@Component({
  selector: 'app-root',
  imports: [RouterLink, RouterLinkActive, RouterOutlet, OutboxHealthComponent],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App {
  private readonly router = inject(Router);
  private readonly outboxHealthService = inject(OutboxHealthService);

  /** SCRUM-186: polled every 30 seconds; when the API cannot answer, nothing is shown rather than a false alarm. */
  readonly outboxHealth = toSignal(
    timer(0, 30_000).pipe(
      switchMap(() => this.outboxHealthService.read().pipe(catchError(() => of(null)))),
    ),
    { initialValue: null },
  );

  readonly chromeless = toSignal(
    this.router.events.pipe(
      filter((event) => event instanceof NavigationEnd),
      map((event) => event.urlAfterRedirects.includes(VENDOR_VIEW_SEGMENT)),
    ),
    { initialValue: this.router.url.includes(VENDOR_VIEW_SEGMENT) },
  );
}
