import { ChangeDetectionStrategy, Component, DestroyRef, ElementRef, Injector, afterNextRender, inject, output, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NavigationStart, Router, RouterLink, RouterLinkActive } from '@angular/router';
import { Store } from '@ngrx/store';
import { filter } from 'rxjs';
import { selectMenuEntries } from '../../features/storefront/navigation';
import { selectBrand } from '../../features/storefront/site-settings';
import { NavItem } from '../../shared/models/site-settings.model';
import { resolveHref } from '../../shared/utils/resolve-href';
import { IconComponent } from '../../shared/ui/icon/icon.component';
import { MegaMenuComponent } from '../mega-menu/mega-menu.component';

/**
 * Sticky header. Entries come from the settings; an entry with members is a button that opens its panel on click or
 * Enter (never on hover). The panels stay in the DOM, hidden, so their links are in the server HTML.
 */
@Component({
  selector: 'rp-site-header',
  imports: [RouterLink, RouterLinkActive, IconComponent, MegaMenuComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './site-header.component.html',
  styleUrl: './site-header.component.scss',
  host: { '(document:click)': 'onDocumentClick($event)', '(keydown.escape)': 'onEscape()' },
})
export class SiteHeaderComponent {
  private readonly store = inject(Store);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef).nativeElement;
  private readonly injector = inject(Injector);

  protected readonly entries = this.store.selectSignal(selectMenuEntries);
  protected readonly brand = this.store.selectSignal(selectBrand);

  /** The key of the entry whose panel is open, or null. */
  protected readonly openKey = signal<string | null>(null);

  readonly menuOpen = output<void>();
  readonly searchOpen = output<void>();

  constructor() {
    inject(Router)
      .events.pipe(filter((e) => e instanceof NavigationStart), takeUntilDestroyed(inject(DestroyRef)))
      .subscribe(() => this.openKey.set(null));
  }

  protected href(item: NavItem): string | null {
    return item.target ? resolveHref(item.target) : null;
  }

  protected toggle(key: string): void {
    if (this.openKey() === key) {
      this.openKey.set(null);
      return;
    }
    this.openKey.set(key);
    afterNextRender(() => this.host.querySelector<HTMLElement>(`#mega-${key} a`)?.focus(), { injector: this.injector });
  }

  protected onEscape(): void {
    const key = this.openKey();
    if (!key) return;
    this.openKey.set(null);
    this.host.querySelector<HTMLElement>(`[aria-controls="mega-${key}"]`)?.focus();
  }

  protected onDocumentClick(event: Event): void {
    if (this.openKey() && event.target instanceof Node && !this.host.contains(event.target)) this.openKey.set(null);
  }
}
