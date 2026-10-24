import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';

/** Save and discard for a settings screen, with a plain-text reminder while edits are unsaved. */
@Component({
  selector: 'app-save-bar',
  template: `
    <div class="form-actions">
      <ng-content />
      <button type="button" class="button button--primary" [disabled]="saving() || !dirty()" (click)="save.emit()">
        {{ saving() ? 'Saving…' : label() }}
      </button>
      <button type="button" class="button" [disabled]="!dirty()" (click)="discard.emit()">Discard changes</button>
      @if (dirty()) {
        <span class="field-hint" role="status">You have changes that are not saved.</span>
      }
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SaveBarComponent {
  readonly label = input('Save');
  readonly saving = input(false);
  readonly dirty = input(false);

  readonly save = output<void>();
  readonly discard = output<void>();
}
