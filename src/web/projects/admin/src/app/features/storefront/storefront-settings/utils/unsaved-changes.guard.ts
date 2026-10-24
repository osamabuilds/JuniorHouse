import { CanDeactivateFn } from '@angular/router';

/** A screen with edits that are not saved yet. */
export interface HasUnsavedChanges {
  hasUnsavedChanges(): boolean;
}

export const UNSAVED_CHANGES_MESSAGE = 'You have changes that are not saved. Leave without saving them?';

/** Asks before leaving an editor with unsaved edits, so a stray click on another tab cannot throw work away. */
export const unsavedChangesGuard: CanDeactivateFn<HasUnsavedChanges> = (component) =>
  !component.hasUnsavedChanges() || window.confirm(UNSAVED_CHANGES_MESSAGE);
