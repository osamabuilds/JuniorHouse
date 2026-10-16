import { Directive, input } from '@angular/core';

/** Uppercase underlined call-to-action link: `<a rpLink href="/girls">Shop girls</a>`. */
@Directive({
  selector: 'a[rpLink]',
  host: { class: 'rp-link', '[class.rp-link--light]': 'light()' },
})
export class LinkCtaDirective {
  readonly light = input(false);
}
