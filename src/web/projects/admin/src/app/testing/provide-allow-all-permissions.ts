import { Provider } from '@angular/core';
import { PermissionAccess } from '@core/auth';

/**
 * For unit tests of a screen or component that contains write buttons: run it as a person who may do everything, so the test can
 * focus on what the screen does. The tests of the hiding itself (has-permission.directive.spec.ts, app.spec.ts) use the real check.
 * A plain object, not a subclass: a subclass would inherit the base class factory (which needs the Store).
 */
export function provideAllowAllPermissions(): Provider {
  return { provide: PermissionAccess, useValue: { allows: () => true } satisfies PermissionAccess };
}
