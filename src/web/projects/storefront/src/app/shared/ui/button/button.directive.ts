import { Directive, input } from '@angular/core';

export type RpBtnVariant = 'primary' | 'ghost' | 'light';

/**
 * Design-system button on a native `<button>` or `<a>`: `<button rpBtn>`, `<button rpBtn="ghost" size="sm">`.
 * Pass `reasonId` (the id of the nearby text) when disabled so the reason is announced.
 */
@Directive({
  selector: 'button[rpBtn], a[rpBtn]',
  host: {
    class: 'rp-btn',
    '[class.rp-btn--ghost]': 'rpBtn() === "ghost"',
    '[class.rp-btn--light]': 'rpBtn() === "light"',
    '[class.rp-btn--sm]': 'size() === "sm"',
    '[class.rp-btn--block]': 'block()',
    '[attr.aria-describedby]': 'reasonId() || null',
  },
})
export class ButtonDirective {
  readonly rpBtn = input<RpBtnVariant | ''>('primary');
  readonly size = input<'md' | 'sm'>('md');
  readonly block = input(false);
  readonly reasonId = input('');
}
