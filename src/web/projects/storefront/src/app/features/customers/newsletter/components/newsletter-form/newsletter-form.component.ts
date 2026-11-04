import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { Store } from '@ngrx/store';
import { ButtonDirective } from '../../../../../shared/ui/button/button.directive';
import { NewsletterActions } from '../../store/newsletter.actions';
import { selectNewsletterFooter } from '../../store/newsletter.reducer';

const MESSAGES = {
  idle: '',
  sending: 'Sending...',
  done: 'Thank you. Check your inbox for a welcome email.',
  invalid: 'That does not look like an email address. Please check it and try again.',
  busy: 'Too many tries. Please wait a minute and try again.',
  error: 'Something went wrong. Please try again.',
} as const;

/**
 * The footer subscribe form (NLS-1, NLS-6). It is a real form that posts to the storefront server, so it works without JavaScript; with
 * JavaScript the same fields go through the store and the answer is announced in a live region. The answer is the same for a new and an
 * already-subscribed address, so the form never shows which one it was.
 */
@Component({
  selector: 'rp-newsletter-form',
  imports: [ButtonDirective],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './newsletter-form.component.html',
})
export class NewsletterFormComponent {
  private readonly store = inject(Store);
  protected readonly status = this.store.selectSignal(selectNewsletterFooter);
  protected readonly message = computed(() => MESSAGES[this.status()]);
  protected readonly invalid = computed(() => this.status() === 'invalid');

  protected submit(event: Event, field: HTMLInputElement): void {
    event.preventDefault();
    if (!field.reportValidity()) return;
    this.store.dispatch(NewsletterActions.subscribe({ email: field.value.trim() }));
  }

  /** Typing again after an answer clears it, so an old message never sits beside a new address. */
  protected edited(): void {
    if (this.status() !== 'idle' && this.status() !== 'sending') this.store.dispatch(NewsletterActions.resetFooter());
  }
}
