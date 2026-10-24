import { NgTemplateOutlet } from '@angular/common';
import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { RouterLink } from '@angular/router';
import { NavItem } from '../../shared/models/site-settings.model';
import { resolveHref } from '../../shared/utils/resolve-href';

/**
 * One level of the navigation tree, recursive. `list` shows members as nested lists (mega-menu column);
 * `accordion` wraps members that have members in native details/summary (mobile menu). No URL is built here.
 */
@Component({
  selector: 'rp-nav-tree',
  imports: [RouterLink, NgTemplateOutlet],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './nav-tree.component.html',
  styleUrl: './nav-tree.component.scss',
})
export class NavTreeComponent {
  readonly items = input.required<readonly NavItem[]>();
  readonly mode = input<'list' | 'accordion'>('list');
  /** Fired when a link is chosen, so the host can close its panel or dialog. */
  readonly navigate = output<void>();

  protected href(item: NavItem): string | null {
    return item.target ? resolveHref(item.target) : null;
  }
  protected internal(href: string): boolean {
    return href.startsWith('/');
  }
}
