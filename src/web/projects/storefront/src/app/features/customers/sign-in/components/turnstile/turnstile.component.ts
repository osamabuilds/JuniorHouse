import { DOCUMENT } from '@angular/common';
import { ChangeDetectionStrategy, Component, ElementRef, OnDestroy, afterNextRender, inject, input, output, viewChild } from '@angular/core';
import { DEV_CAPTCHA_TOKEN } from '../../../../../core/customer-auth/models/customer-auth.model';

interface TurnstileApi {
  render(container: HTMLElement, options: Record<string, unknown>): string;
  remove(widgetId: string): void;
  reset(widgetId: string): void;
}

const SCRIPT = 'https://challenges.cloudflare.com/turnstile/v0/api.js?render=explicit';

/**
 * The Cloudflare Turnstile check (SEC-02), shown only where the API asked for it. The script is loaded when this component appears, so no
 * other page pays for it. With no site key (a local run) the widget is skipped and the API's development token is passed on instead; the real
 * verifier rejects that token, so it cannot be used to get round the check in production.
 */
@Component({
  selector: 'rp-turnstile',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<div #host class="rp-captcha" aria-live="polite"></div>`,
})
export class TurnstileComponent implements OnDestroy {
  private readonly doc = inject(DOCUMENT);
  private readonly host = viewChild.required<ElementRef<HTMLElement>>('host');
  private widgetId: string | null = null;

  readonly siteKey = input('');
  /** The one-time token, or null when it expired or the widget was reset. */
  readonly token = output<string | null>();

  constructor() {
    afterNextRender(() => {
      if (!this.siteKey()) {
        this.token.emit(DEV_CAPTCHA_TOKEN);
        return;
      }
      void this.load().then(() => this.render());
    });
  }

  /** A token works once; after a failed attempt the customer needs a new one. */
  reset(): void {
    if (this.widgetId) (this.win().turnstile as TurnstileApi | undefined)?.reset(this.widgetId);
    else if (!this.siteKey()) this.token.emit(DEV_CAPTCHA_TOKEN);
  }

  ngOnDestroy(): void {
    if (this.widgetId) (this.win().turnstile as TurnstileApi | undefined)?.remove(this.widgetId);
  }

  private win(): Window & { turnstile?: unknown } {
    return this.doc.defaultView as Window & { turnstile?: unknown };
  }

  private load(): Promise<void> {
    if (this.win().turnstile) return Promise.resolve();
    return new Promise((resolve, reject) => {
      const existing = this.doc.querySelector<HTMLScriptElement>(`script[src="${SCRIPT}"]`);
      const script = existing ?? this.doc.createElement('script');
      script.addEventListener('load', () => resolve(), { once: true });
      script.addEventListener('error', () => reject(new Error('turnstile')), { once: true });
      if (!existing) {
        script.src = SCRIPT;
        script.async = true;
        this.doc.head.appendChild(script);
      }
    });
  }

  private render(): void {
    const api = this.win().turnstile as TurnstileApi | undefined;
    if (!api) return;
    this.widgetId = api.render(this.host().nativeElement, {
      sitekey: this.siteKey(),
      callback: (t: string) => this.token.emit(t),
      'expired-callback': () => this.token.emit(null),
      'error-callback': () => this.token.emit(null),
    });
  }
}
