import { ChangeDetectionStrategy, Component, RESPONSE_INIT, computed, effect, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Store } from '@ngrx/store';
import { map } from 'rxjs';
import { SeoService } from '../../../../../shared/seo/seo.service';
import { selectBrand, selectFacts, selectTopLinks } from '../../../site-settings';
import { PageResult } from '../../services/page-api.service';
import { PageActions } from '../../store/page.actions';
import { selectPageResults } from '../../store/page.selectors';
import { PageBlockComponent } from '../../components/page-block/page-block.component';

const STATUS: Record<PageResult['status'], number> = { ok: 200, notFound: 404, gone: 410, error: 503 };

/** Shows one content page by slug, with the real HTTP status for pages that are missing, removed or failed. */
@Component({
  selector: 'rp-page',
  imports: [RouterLink, PageBlockComponent],
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
  private readonly brand = this.store.selectSignal(selectBrand);
  private readonly results = this.store.selectSignal(selectPageResults);

  protected readonly result = computed((): PageResult | null => {
    const slug = this.slug();
    if (!slug) return { status: 'notFound' };
    return this.results()[slug] ?? null;
  });

  constructor() {
    effect(() => {
      const slug = this.slug();
      if (slug && !this.results()[slug]) this.store.dispatch(PageActions.load({ slug }));
    });

    effect(() => {
      const r = this.result();
      if (!r) return;
      if (this.responseInit) this.responseInit.status = STATUS[r.status];
      const siteName = this.brand().name;
      if (r.status === 'ok') {
        const path = `/${r.page.slug}`;
        this.seo.set({ title: r.page.title, description: r.page.description, path, siteName, crumbs: [{ name: 'Home', path: '/' }, { name: r.page.title, path }] });
      } else {
        this.seo.set({ title: r.status === 'error' ? 'Something went wrong' : 'Page not found', description: 'This page is not available.', path: this.slug() ? `/${this.slug()}` : '/', siteName, crumbs: [], noindex: true });
      }
    });
  }
}
