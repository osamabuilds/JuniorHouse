import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, ElementRef, computed, inject, signal, viewChild } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Store } from '@ngrx/store';
import { SearchSuggestActions, SuggestionItem, selectSuggestions } from '../../features/storefront/search';
import { selectHeaderEntries, selectPopularSearches } from '../../features/storefront/site-settings';
import { IconComponent } from '../../shared/ui/icon/icon.component';
import { resolveHref } from '../../shared/utils/resolve-href';
import { RpDialog } from '../dialog/dialog.directive';

/**
 * Full-screen search on a native dialog. The form is a plain GET to /search, so Enter works without scripting;
 * suggestions arrive 200 ms after the last keystroke and are always rendered as text.
 */
@Component({
  selector: 'rp-search-overlay',
  imports: [RpDialog, RouterLink, IconComponent, DecimalPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './search-overlay.component.html',
})
export class SearchOverlayComponent {
  private readonly store = inject(Store);
  private readonly dialog = viewChild.required(RpDialog);
  private readonly input = viewChild.required<ElementRef<HTMLInputElement>>('field');

  protected readonly popular = this.store.selectSignal(selectPopularSearches);
  protected readonly suggestions = this.store.selectSignal(selectSuggestions);
  private readonly entries = this.store.selectSignal(selectHeaderEntries);
  protected readonly links = () =>
    this.entries().flatMap((e) => {
      const href = e.target ? resolveHref(e.target) : null;
      return href ? [{ label: e.label, href }] : [];
    });

  protected readonly query = signal('');

  protected readonly categories = computed(() => this.suggestions().results.filter((s) => s.kind === 'category'));
  protected readonly products = computed(() => this.suggestions().results.filter((s) => s.kind === 'product'));

  /** The smallest generated picture is plenty for a thumbnail. */
  protected thumb(item: SuggestionItem): { src: string; width: number; height: number; alt: string } | null {
    const image = item.image;
    const source = image?.sources[0];
    return image && source ? { src: source.jpeg ?? source.webp, width: image.width, height: image.height, alt: image.altText } : null;
  }

  open(): void {
    this.dialog().open();
    this.input().nativeElement.focus();
  }

  close(): void {
    this.dialog().close();
  }

  protected onInput(event: Event): void {
    const value = (event.target as HTMLInputElement).value;
    this.query.set(value);
    this.store.dispatch(SearchSuggestActions.queryChanged({ query: value }));
  }

  protected reset(): void {
    this.query.set('');
    this.input().nativeElement.value = '';
    this.store.dispatch(SearchSuggestActions.queryChanged({ query: '' }));
  }

  /** Arrow down moves from the field to the first suggestion; arrow up from the first suggestion back. */
  protected focusFirst(event: Event): void {
    const first = (this.input().nativeElement.closest('dialog') as HTMLElement).querySelector<HTMLElement>('.rp-suggestions a');
    if (first) {
      event.preventDefault();
      first.focus();
    }
  }
  protected onSuggestionKey(event: Event, index: number): void {
    const list = [...(this.input().nativeElement.closest('dialog') as HTMLElement).querySelectorAll<HTMLElement>('.rp-suggestions a')];
    const key = (event as KeyboardEvent).key;
    if (key === 'ArrowDown' && list[index + 1]) { event.preventDefault(); list[index + 1].focus(); }
    if (key === 'ArrowUp') { event.preventDefault(); (index === 0 ? this.input().nativeElement : list[index - 1]).focus(); }
  }
}
