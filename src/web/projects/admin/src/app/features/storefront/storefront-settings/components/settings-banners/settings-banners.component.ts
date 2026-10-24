import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { ApiError } from '@core/http';

/**
 * The messages every settings screen shows the same way: what was saved, why a load or save failed, and, for a conflict, a way to
 * reload (ST-14). A refusal that comes with field errors points the reader at the marked fields instead of repeating them all.
 */
@Component({
  selector: 'app-settings-banners',
  template: `
    @if (notice()) {
      <p class="banner banner--success" role="status">
        {{ notice() }}
        <button type="button" class="button button--small" (click)="dismissed.emit()">Dismiss</button>
      </p>
    }
    @if (loadError()) {
      <p class="banner banner--error" role="alert">{{ loadError()?.message }}</p>
    }
    @if (saveError(); as error) {
      <div class="banner banner--error" role="alert">
        @if (error.status === 409) {
          <p>{{ error.message }}</p>
          <button type="button" class="button button--small" (click)="reload.emit()">Reload the latest settings</button>
        } @else if (hasFields()) {
          <p>This can't be saved yet. Fix what is marked below and save again.</p>
        } @else {
          <p>{{ error.message }}</p>
        }
        @for (message of general(); track message) {
          <p>{{ message }}</p>
        }
      </div>
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SettingsBannersComponent {
  readonly notice = input<string | null>(null);
  readonly loadError = input<ApiError | null>(null);
  readonly saveError = input<ApiError | null>(null);
  /** Field names whose messages are shown as a banner line because no row carries them (for example `navigation` or `popularSearches`). */
  readonly generalFields = input<readonly string[]>([]);

  readonly dismissed = output<void>();
  readonly reload = output<void>();

  readonly hasFields = computed(() => Object.keys(this.saveError()?.fieldErrors ?? {}).length > 0);
  readonly general = computed(() => this.generalFields().flatMap((field) => this.saveError()?.fieldErrors[field] ?? []));
}
