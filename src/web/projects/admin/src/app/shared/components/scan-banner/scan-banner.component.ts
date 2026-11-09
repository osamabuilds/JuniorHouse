import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/**
 * The large answer to a scan (APP-4): a word, an icon and a colour, never colour alone (NFR-19), announced politely to screen readers. A refusal
 * is told in words; the optional sound or vibration is the device's own and is not required.
 */
@Component({
  selector: 'app-scan-banner',
  templateUrl: './scan-banner.component.html',
  styleUrl: './scan-banner.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ScanBannerComponent {
  readonly accepted = input.required<boolean>();
  readonly message = input.required<string>();
}
