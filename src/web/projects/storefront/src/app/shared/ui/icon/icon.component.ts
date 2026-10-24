import { ChangeDetectionStrategy, Component, input } from '@angular/core';

export type RpIconName =
  | 'search'
  | 'heart'
  | 'bag'
  | 'menu'
  | 'close'
  | 'arrow'
  | 'arrow-left'
  | 'check'
  | 'alert'
  | 'truck'
  | 'swap'
  | 'chat'
  | 'cash'
  | 'pin';

/** Thin outline icons on a 24px grid (design system). `chat` is a stand-in: the previews do not draw it. */
@Component({
  selector: 'rp-icon',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { style: 'display: inline-flex' },
  template: `
    <svg
      class="rp-ico"
      viewBox="0 0 24 24"
      [attr.aria-hidden]="label() ? null : 'true'"
      [attr.role]="label() ? 'img' : null"
      [attr.aria-label]="label() || null"
    >
      @switch (name()) {
        @case ('search') {
          <circle cx="11" cy="11" r="6.5" />
          <path d="m16 16 4.5 4.5" />
        }
        @case ('heart') {
          <path
            d="M12 20s-7.5-4.6-7.5-10.2A4.3 4.3 0 0 1 12 7.4a4.3 4.3 0 0 1 7.5 2.4C19.5 15.4 12 20 12 20Z"
          />
        }
        @case ('bag') {
          <path d="M5.5 8.5h13l-1 11h-11z" />
          <path d="M9 8.5V7a3 3 0 0 1 6 0v1.5" />
        }
        @case ('menu') {
          <path d="M4 8h16M4 16h16" />
        }
        @case ('close') {
          <path d="m6 6 12 12M18 6 6 18" />
        }
        @case ('arrow') {
          <path d="M4 12h16m-5-5 5 5-5 5" />
        }
        @case ('arrow-left') {
          <path d="M20 12H4m5-5-5 5 5 5" />
        }
        @case ('check') {
          <path d="m5 12.5 4.5 4.5L19 7.5" />
        }
        @case ('alert') {
          <circle cx="12" cy="12" r="8.5" />
          <path d="M12 7.5v5.5M12 16.2v.1" />
        }
        @case ('truck') {
          <path d="M3 7h11v9H3zM14 10h4l3 3v3h-7" />
          <circle cx="7.5" cy="17.5" r="1.5" />
          <circle cx="17.5" cy="17.5" r="1.5" />
        }
        @case ('swap') {
          <path d="M4 9h14l-3-3M20 15H6l3 3" />
        }
        @case ('chat') {
          <path d="M5 5h14a1 1 0 0 1 1 1v9a1 1 0 0 1-1 1H10l-4 4v-4H5a1 1 0 0 1-1-1V6a1 1 0 0 1 1-1z" />
        }
        @case ('cash') {
          <rect x="3" y="7" width="18" height="10" rx="1" />
          <circle cx="12" cy="12" r="2.5" />
        }
        @case ('pin') {
          <path d="M12 21s-6-5.6-6-10a6 6 0 0 1 12 0c0 4.4-6 10-6 10z" />
          <circle cx="12" cy="11" r="2" />
        }
      }
    </svg>
  `,
})
export class IconComponent {
  readonly name = input.required<RpIconName>();
  /** Set only when the icon carries meaning on its own; otherwise it is decorative. */
  readonly label = input<string>('');
}
