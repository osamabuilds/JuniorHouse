import { ChangeDetectionStrategy, Component, RESPONSE_INIT, computed, effect, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Store } from '@ngrx/store';
import { map } from 'rxjs';
import { SeoService } from '../../../../../shared/seo/seo.service';
import { selectBrand } from '../../../../storefront/site-settings';
import { ResultKind, ResultStatus } from '../../models/newsletter.model';

const COPY: Record<ResultKind, Record<ResultStatus, { title: string; body: string }>> = {
  subscribed: {
    ok: { title: 'You are subscribed', body: 'Thank you. We have sent you a welcome email with a link to unsubscribe at any time.' },
    invalid: { title: 'That email could not be read', body: 'Please go back and check the address, then try again.' },
    error: { title: 'We could not subscribe you', body: 'Something went wrong on our side. Please try again in a few minutes.' },
  },
  unsubscribed: {
    ok: { title: 'You are unsubscribed', body: 'You will not receive our newsletter any more. You can subscribe again whenever you like.' },
    invalid: { title: 'This link is not valid', body: 'The unsubscribe link may be incomplete. Open the link from the email again, or manage the newsletter in your account.' },
    error: { title: 'We could not unsubscribe you', body: 'Something went wrong on our side. Please open the link again in a few minutes.' },
  },
};

/**
 * Where the storefront server sends a visitor after the footer form without JavaScript (`/newsletter/subscribed`) and after an unsubscribe
 * link (`/newsletter/unsubscribed`). The server has already done the work and put the outcome in the address; this page only says it.
 * It is for one person, so it is `noindex` and never cached (SEO-05, NFR-26).
 */
@Component({
  selector: 'rp-newsletter-result-page',
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './newsletter-result-page.container.html',
})
export class NewsletterResultPageContainer {
  private readonly route = inject(ActivatedRoute);
  private readonly seo = inject(SeoService);
  private readonly store = inject(Store);
  private readonly responseInit = inject(RESPONSE_INIT, { optional: true });
  private readonly brand = this.store.selectSignal(selectBrand);

  private readonly kind: ResultKind = this.route.snapshot.data['kind'] === 'unsubscribed' ? 'unsubscribed' : 'subscribed';
  private readonly status = toSignal(
    this.route.queryParamMap.pipe(map((q): ResultStatus => (q.get('status') === 'ok' ? 'ok' : q.get('status') === 'invalid' ? 'invalid' : 'error'))),
    { initialValue: 'error' as ResultStatus },
  );
  protected readonly copy = computed(() => COPY[this.kind][this.status()]);
  protected readonly ok = computed(() => this.status() === 'ok');

  constructor() {
    this.noCache();
    effect(() => {
      this.seo.set({
        title: this.copy().title,
        description: this.copy().body,
        path: `/newsletter/${this.kind}`,
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
