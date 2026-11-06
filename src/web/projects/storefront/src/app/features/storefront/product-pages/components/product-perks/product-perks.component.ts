import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { IconComponent } from '../../../../../shared/ui/icon/icon.component';
import { Perk } from '../../utils/perks.util';

/** The short promises under the buy button (BUY-10, BUY-11). Presentational: the wording comes from the settings through `buildPerks`. */
@Component({
  selector: 'rp-product-perks',
  imports: [IconComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (perks().length > 0) {
      <ul class="rp-perks">
        @for (perk of perks(); track perk.text) {
          <li>
            <rp-icon [name]="perk.icon" />
            @if (perk.href; as href) {
              <a class="rp-link" [href]="href" rel="noopener">{{ perk.text }}</a>
            } @else {
              <span>{{ perk.text }}</span>
            }
          </li>
        }
      </ul>
    }
  `,
})
export class ProductPerksComponent {
  readonly perks = input.required<readonly Perk[]>();
}
