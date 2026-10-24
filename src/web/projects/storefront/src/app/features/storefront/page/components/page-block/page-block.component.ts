import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { PageBlock } from '../../../../../shared/models/site-settings.model';
import { FactsView } from '../../../site-settings';

const EMAIL = /^[^\s@"'<>]+@[^\s@"'<>]+\.[^\s@"'<>]+$/;

type Of<T extends PageBlock['type']> = Extract<PageBlock, { type: T }>;

/** One content block, rendered through bindings only (plain text, never markup). Presentational. */
@Component({
  selector: 'rp-page-block',
  imports: [DecimalPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './page-block.component.html',
  styleUrl: './page-block.component.scss',
})
export class PageBlockComponent {
  readonly block = input.required<PageBlock>();
  readonly facts = input.required<FactsView>();

  protected readonly heading = computed(() => this.pick<Of<'heading'>>('heading'));
  protected readonly paragraph = computed(() => this.pick<Of<'paragraph'>>('paragraph'));
  protected readonly list = computed(() => this.pick<Of<'list'>>('list'));
  protected readonly table = computed(() => this.pick<Of<'table'>>('table'));
  protected readonly callout = computed(() => this.pick<Of<'callout'>>('callout'));

  /** The fact to show, or null when it has not been entered (then the whole block, heading included, is omitted). */
  protected readonly fact = computed(() => {
    const b = this.pick<Of<'fact'>>('fact');
    if (!b) return null;
    const f = this.facts();
    const label = b.label ?? '';
    switch (b.key) {
      case 'contact.whatsapp': return f.whatsapp ? { label, kind: 'link' as const, href: f.whatsapp.href, text: f.whatsapp.display } : null;
      case 'contact.email': return f.email ? (EMAIL.test(f.email) ? { label, kind: 'link' as const, href: `mailto:${f.email}`, text: f.email } : { label, kind: 'text' as const, text: f.email }) : null;
      case 'contact.hours': return f.hours ? { label, kind: 'text' as const, text: f.hours } : null;
      case 'exchange.window': return f.exchangeWindowDays ? { label, kind: 'text' as const, text: `${f.exchangeWindowDays} days` } : null;
      case 'exchange.conditions': return f.exchangeConditions.length > 0 ? { label, kind: 'list' as const, items: f.exchangeConditions } : null;
      case 'delivery.rules': return f.deliveryRules.length > 0 ? { label, kind: 'delivery' as const, rules: f.deliveryRules } : null;
      default: return null;
    }
  });

  private pick<T extends PageBlock>(type: T['type']): T | null {
    const b = this.block();
    return b.type === type ? (b as T) : null;
  }
}
