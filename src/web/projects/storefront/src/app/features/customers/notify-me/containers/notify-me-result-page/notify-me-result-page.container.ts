import { ChangeDetectionStrategy, Component, RESPONSE_INIT, computed, effect, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Store } from '@ngrx/store';
import { map } from 'rxjs';
import { SeoService } from '../../../../../shared/seo/seo.service';
import { selectBrand } from '../../../../storefront/site-settings';
import { NotifyResultStatus } from '../../models/notify-me.model';

const COPY: Record<NotifyResultStatus, { title: string; body: string }> = {
  ok: { title: 'We will let you know', body: 'Thank you. We will email you once when that size is back in stock.' },
  invalid: { title: 'We could not save that request', body: 'The email address could not be read, or the size is in stock now. Please go back and try again.' },
  error: { title: 'We could not save that request', body: 'Something went wrong on our side. Please try again in a few minutes.' },
};

/**
 * Where the storefront server sends a visitor after the Notify Me form without JavaScript (`/notify-me/requested`). The server has already
 * done the work and put the outcome in the address; this page only says it. It is for one person, so it is `noindex` and never cached.
 */
@Component({
  selector: 'rp-notify-me-result-page',
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './notify-me-result-page.container.html',
})
export class NotifyMeResultPageContainer {
  private readonly route = inject(ActivatedRoute);
  private readonly seo = inject(SeoService);
  private readonly store = inject(Store);
  private readonly responseInit = inject(RESPONSE_INIT, { optional: true });
  private readonly brand = this.store.selectSignal(selectBrand);

  private readonly status = toSignal(
    this.route.queryParamMap.pipe(map((q): NotifyResultStatus => (q.get('status') === 'ok' ? 'ok' : q.get('status') === 'invalid' ? 'invalid' : 'error'))),
    { initialValue: 'error' as NotifyResultStatus },
  );
  protected readonly copy = computed(() => COPY[this.status()]);
  protected readonly ok = computed(() => this.status() === 'ok');

  constructor() {
    this.noCache();
    effect(() => {
      this.seo.set({
        title: this.copy().title,
        description: this.copy().body,
        path: '/notify-me/requested',
        siteName: this.brand().name,
        crumbs: [],
        noindex: true,
      });
    });
  }

  private noCache(): void {
    if (!this.responseInit) return;
    const headers = new Headers(this.responseInit.headers);
    headers.set('Cache-Control', 'private, no-store');
    this.responseInit.headers = headers;
  }
}
