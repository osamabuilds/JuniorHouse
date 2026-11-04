import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { Store } from '@ngrx/store';
import { ButtonDirective } from '../../../../../shared/ui/button/button.directive';
import { NotifyMeActions } from '../../store/notify-me.actions';
import { notifyMeFeature } from '../../store/notify-me.reducer';

/**
 * "Tell me when this size is back" for one sold-out size (FR-11, NTM-1, NTM-10). It is a real form that posts to the storefront server, so
 * it works without JavaScript; with JavaScript the same fields go through the store and the answer is announced in a live region. The
 * answer is the same for a new and a repeated address (NTM-3).
 */
@Component({
  selector: 'rp-notify-me-form',
  imports: [ButtonDirective],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './notify-me-form.component.html',
  styles: `
    .notify { margin-top: var(--space-4); padding: var(--space-4); border: 1px solid var(--line-strong); border-radius: var(--radius-sm); }
    .notify__title { margin: 0 0 var(--space-3); font: 500 12px/16px var(--font-sans); letter-spacing: 0.14em; text-transform: uppercase; }
  `,
})
export class NotifyMeFormComponent {
  private readonly store = inject(Store);

  /** The size (catalogue variant) the shopper chose. */
  readonly variantId = input.required<number>();
  readonly sizeName = input.required<string>();

  private readonly forms = this.store.selectSignal(notifyMeFeature.selectForms);
  protected readonly form = computed(() => this.forms()[this.variantId()] ?? null);
  protected readonly sending = computed(() => this.form()?.status === 'sending');
  protected readonly invalid = computed(() => this.form()?.status === 'invalid');
  protected readonly fieldId = computed(() => `rp-notify-email-${this.variantId()}`);
  protected readonly message = computed(() => {
    const form = this.form();
    switch (form?.status) {
      case 'sending':
        return 'Sending...';
      case 'done':
        return `Thank you. We will email you once when size ${this.sizeName()} is back.`;
      case 'invalid':
        return form.message;
      case 'busy':
        return 'Too many tries. Please wait a minute and try again.';
      case 'error':
        return 'Something went wrong. Please try again.';
      default:
        return '';
    }
  });

  protected submit(event: Event, field: HTMLInputElement): void {
    event.preventDefault();
    if (!field.reportValidity()) return;
    this.store.dispatch(NotifyMeActions.request({ variantId: this.variantId(), email: field.value.trim() }));
  }

  protected edited(): void {
    const status = this.form()?.status;
    if (status && status !== 'sending') this.store.dispatch(NotifyMeActions.reset({ variantId: this.variantId() }));
  }
}
