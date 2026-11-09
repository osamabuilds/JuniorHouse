import { ChangeDetectionStrategy, Component, RESPONSE_INIT, computed, effect, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Store } from '@ngrx/store';
import { map } from 'rxjs';
import { NotFoundComponent } from '../../../../../shared/ui/not-found/not-found.component';
import { SeoService } from '../../../../../shared/seo/seo.service';
import { selectBrand, selectFacts, selectHelpPages, selectTopLinks } from '../../../site-settings';
import { PageResult } from '../../services/page-api.service';
import { PageActions } from '../../store/page.actions';
import { selectPageResults } from '../../store/page.selectors';
import { PageBlockComponent } from '../../components/page-block/page-block.component';

const STATUS: Record<PageResult['status'], number> = { ok: 200, notFound: 404, gone: 410, error: 503 };

/**
 * Terms and Privacy are legal text the business supplies (checkout shows a link to each, NFR-16). Until the owner publishes the real page in the
 * admin app, the link shows this short notice to the shopper instead of the generic "not found" screen, and it replaces itself the moment the
 * real page is published. The HTTP status stays 404 until then (ST-8: a draft page is not served), so search engines never index it, and the
 * notice makes no legal promise of its own.
 */
export const LEGAL_PLACEHOLDERS: Readonly<Record<string, { readonly title: string; readonly description: string }>> = {
  terms: { title: 'Terms and conditions', description: 'Our terms and conditions are being prepared.' },
  privacy: { title: 'Privacy policy', description: 'Our privacy policy is being prepared.' },
};

/** Shows one content page by slug, with the real HTTP status for pages that are missing, removed or failed. */
@Component({
  selector: 'rp-page',
  imports: [RouterLink, NotFoundComponent, PageBlockComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './page.container.html',
  styleUrl: './page.container.scss',
})
export class PageContainer {
  private readonly store = inject(Store);
  private readonly route = inject(ActivatedRoute);
  private readonly seo = inject(SeoService);
  private readonly responseInit = inject(RESPONSE_INIT, { optional: true });

  /** The slug in the URL; null for the catch-all route, which is always a 404. */
  protected readonly slug = toSignal(this.route.paramMap.pipe(map((p) => p.get('slug'))), { initialValue: this.route.snapshot.paramMap?.get('slug') ?? null });
  protected readonly facts = this.store.selectSignal(selectFacts);
  protected readonly links = this.store.selectSignal(selectTopLinks);
  protected readonly helpPages = this.store.selectSignal(selectHelpPages);
  private readonly brand = this.store.selectSignal(selectBrand);
  private readonly results = this.store.selectSignal(selectPageResults);

  protected readonly result = computed((): PageResult | null => {
    const slug = this.slug();
    if (!slug) return { status: 'notFound' };
    return this.results()[slug] ?? null;
  });

  /** The notice shown for a legal page that is not published yet (Terms, Privacy), instead of a not-found. */
  protected readonly placeholder = computed(() => {
    const slug = this.slug();
    return this.result()?.status === 'notFound' && slug && LEGAL_PLACEHOLDERS[slug] ? LEGAL_PLACEHOLDERS[slug] : null;
  });

  constructor() {
    effect(() => {
      const slug = this.slug();
      if (slug && !this.results()[slug]) this.store.dispatch(PageActions.load({ slug }));
    });

    effect(() => {
      const r = this.result();
      if (!r) return;
      const siteName = this.brand().name;
      const holding = this.placeholder();
      if (holding) {
        // A page the business has not supplied yet: the shopper sees a notice, the server still says 404, and it is kept out of search.
        if (this.responseInit) this.responseInit.status = STATUS[r.status];
        const path = `/${this.slug()}`;
        this.seo.set({ title: holding.title, description: holding.description, path, siteName, crumbs: [{ name: 'Home', path: '/' }, { name: holding.title, path }], noindex: true });
        return;
      }
      if (this.responseInit) this.responseInit.status = STATUS[r.status];
      if (r.status === 'ok') {
        const path = `/${r.page.slug}`;
        this.seo.set({ title: r.page.title, description: r.page.description, path, siteName, crumbs: [{ name: 'Home', path: '/' }, { name: r.page.title, path }] });
      } else {
        this.seo.set({ title: r.status === 'error' ? 'Something went wrong' : 'Page not found', description: 'This page is not available.', path: this.slug() ? `/${this.slug()}` : '/', siteName, crumbs: [], noindex: true, error: true });
      }
    });
  }
}
