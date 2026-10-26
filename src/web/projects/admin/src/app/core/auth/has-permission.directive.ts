import { Directive, EmbeddedViewRef, Injectable, TemplateRef, ViewContainerRef, effect, inject, input } from '@angular/core';
import { Store } from '@ngrx/store';
import { holdsAny, selectPermissions, selectSessionUser } from './store/auth.selectors';

/**
 * Answers "may this person do this?" for hiding purposes only. It reads signals, so a caller inside an <code>effect</code> or a
 * template re-evaluates when the session changes. The default reads the signed-in session; unit tests of presentational components
 * replace it with an allow-all version (see <code>test-setup.ts</code>), and the specs that test the hiding itself use this one.
 */
@Injectable({ providedIn: 'root', useFactory: () => new StorePermissionAccess(inject(Store)) })
export abstract class PermissionAccess {
  abstract allows(wanted: readonly string[]): boolean;
}

export class StorePermissionAccess extends PermissionAccess {
  private readonly permissions;
  private readonly user;

  constructor(store: Store) {
    super();
    this.permissions = store.selectSignal(selectPermissions);
    this.user = store.selectSignal(selectSessionUser);
  }

  allows(wanted: readonly string[]): boolean {
    return this.user() !== null && holdsAny(this.permissions(), wanted);
  }
}

/**
 * Shows an element only to a signed-in person who holds the permission (or any one of several): <code>*hasPermission="'catalog.write'"</code>.
 * This only keeps screens tidy. It is never a protection: the server checks the permission again on every request (roles AC-7, SEC-08).
 */
@Directive({ selector: '[hasPermission]' })
export class HasPermissionDirective {
  private readonly access = inject(PermissionAccess);
  private readonly template = inject<TemplateRef<unknown>>(TemplateRef);
  private readonly container = inject(ViewContainerRef);
  private view: EmbeddedViewRef<unknown> | null = null;

  readonly required = input.required<string | readonly string[]>({ alias: 'hasPermission' });

  constructor() {
    effect(() => {
      const required = this.required();
      const wanted = typeof required === 'string' ? [required] : required;
      const show = this.access.allows(wanted);

      if (show && !this.view) {
        this.view = this.container.createEmbeddedView(this.template);
      } else if (!show && this.view) {
        this.container.clear();
        this.view = null;
      }
    });
  }
}
