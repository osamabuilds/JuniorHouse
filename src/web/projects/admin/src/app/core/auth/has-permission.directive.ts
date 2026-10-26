import { Directive, EmbeddedViewRef, TemplateRef, ViewContainerRef, effect, inject, input } from '@angular/core';
import { Store } from '@ngrx/store';
import { holdsAny, selectPermissions, selectSessionUser } from './store/auth.selectors';

/**
 * Shows an element only to a signed-in person who holds the permission (or any one of several): <code>*hasPermission="'catalog.write'"</code>.
 * This only keeps screens tidy. It is never a protection: the server checks the permission again on every request (roles AC-7, SEC-08).
 */
@Directive({ selector: '[hasPermission]' })
export class HasPermissionDirective {
  private readonly store = inject(Store);
  private readonly template = inject<TemplateRef<unknown>>(TemplateRef);
  private readonly container = inject(ViewContainerRef);
  private view: EmbeddedViewRef<unknown> | null = null;

  readonly required = input.required<string | readonly string[]>({ alias: 'hasPermission' });

  private readonly permissions = this.store.selectSignal(selectPermissions);
  private readonly user = this.store.selectSignal(selectSessionUser);

  constructor() {
    effect(() => {
      const required = this.required();
      const wanted = typeof required === 'string' ? [required] : required;
      const show = this.user() !== null && holdsAny(this.permissions(), wanted);

      if (show && !this.view) {
        this.view = this.container.createEmbeddedView(this.template);
      } else if (!show && this.view) {
        this.container.clear();
        this.view = null;
      }
    });
  }
}
