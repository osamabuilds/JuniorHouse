import { ChangeDetectionStrategy, Component, inject, viewChild } from '@angular/core';
import { Store } from '@ngrx/store';
import { selectMenuEntries } from '../../features/storefront/navigation';
import { IconComponent } from '../../shared/ui/icon/icon.component';
import { RpDialog } from '../dialog/dialog.directive';
import { NavTreeComponent } from '../nav-tree/nav-tree.component';

/** Left drawer on a native dialog: the same navigation tree as nested accordions, with search at the top. */
@Component({
  selector: 'rp-mobile-menu',
  imports: [RpDialog, NavTreeComponent, IconComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './mobile-menu.component.html',
})
export class MobileMenuComponent {
  protected readonly entries = inject(Store).selectSignal(selectMenuEntries);
  private readonly dialog = viewChild.required(RpDialog);

  open(): void {
    this.dialog().open();
  }
  close(): void {
    this.dialog().close();
  }
}
