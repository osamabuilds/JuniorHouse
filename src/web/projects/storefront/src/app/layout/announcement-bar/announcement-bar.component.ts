import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { Store } from '@ngrx/store';
import { selectAnnouncement } from '../../features/storefront/site-settings';

/** One line above the header, from the settings. Hidden when switched off or empty. */
@Component({
  selector: 'rp-announcement-bar',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `@if (text(); as t) {<p class="rp-ann">{{ t }}</p>}`,
  styles: ':host { display: block; } p { margin: 0; }',
})
export class AnnouncementBarComponent {
  protected readonly text = inject(Store).selectSignal(selectAnnouncement);
}
