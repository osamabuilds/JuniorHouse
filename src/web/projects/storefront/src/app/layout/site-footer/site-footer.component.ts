import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Store } from '@ngrx/store';
import { selectBrand, selectFooter } from '../../features/storefront/site-settings';

/** Dark footer: link columns, contact line and legal row, all from the settings. No newsletter in this release. */
@Component({
  selector: 'rp-site-footer',
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './site-footer.component.html',
  styleUrl: './site-footer.component.scss',
})
export class SiteFooterComponent {
  private readonly store = inject(Store);
  protected readonly columns = this.store.selectSignal(selectFooter);
  protected readonly brand = this.store.selectSignal(selectBrand);
  protected readonly year = new Date().getFullYear();

  /** International digits only (8 to 15). Anything else shows no link, so a typo never produces a broken one. */
  protected readonly whatsapp = computed(() => {
    const digits = this.brand().whatsapp;
    if (!/^\d{8,15}$/.test(digits)) return null;
    return { href: `https://wa.me/${digits}`, display: `+${digits.slice(0, 2)} ${digits.slice(2, 5)} ${digits.slice(5)}` };
  });

  protected internal(href: string): boolean {
    return href.startsWith('/');
  }
}
