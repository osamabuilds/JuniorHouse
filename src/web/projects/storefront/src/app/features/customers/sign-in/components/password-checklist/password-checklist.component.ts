import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { IconComponent } from '../../../../../shared/ui/icon/icon.component';
import { checkPassword } from '../../utils/password-rules.util';

/**
 * The live checklist under a new-password field (ACT-2): each rule is shown met or not met as the customer types, so they can see which one
 * is missing. Met is told by an icon and the words "Met" / "Not met" for screen readers, never by colour alone.
 */
@Component({
  selector: 'rp-password-checklist',
  imports: [IconComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <ul class="rp-rules" aria-label="Password rules">
      @for (rule of rules(); track rule.code) {
        <li [class.is-met]="rule.met">
          <rp-icon [name]="rule.met ? 'check' : 'alert'" />
          <span>{{ rule.message }}<span class="sr-only"> {{ rule.met ? 'met' : 'not met' }}</span></span>
        </li>
      }
    </ul>
  `,
})
export class PasswordChecklistComponent {
  readonly password = input('');
  protected readonly rules = computed(() => checkPassword(this.password()));
}
