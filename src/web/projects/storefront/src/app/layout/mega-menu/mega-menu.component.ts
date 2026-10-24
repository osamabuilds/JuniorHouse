import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AgeStep, NavItem } from '../../shared/models/site-settings.model';
import { resolveHref } from '../../shared/utils/resolve-href';
import { NavTreeComponent } from '../nav-tree/nav-tree.component';

/** The panel under the header for one parent entry: its members with their further members, and optionally the ages. */
@Component({
  selector: 'rp-mega-menu',
  imports: [NavTreeComponent, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './mega-menu.component.html',
  styleUrl: './mega-menu.component.scss',
})
export class MegaMenuComponent {
  readonly entry = input.required<NavItem>();
  readonly ages = input<readonly AgeStep[]>([]);
  /** A link was chosen: the host closes the panel. */
  readonly navigate = output<void>();

  protected readonly entryHref = computed(() => (this.entry().target ? resolveHref(this.entry().target!) : null));
  protected readonly showAges = computed(() => this.entry().showAges === true && this.ages().length > 0 && this.entryHref() !== null);
}
