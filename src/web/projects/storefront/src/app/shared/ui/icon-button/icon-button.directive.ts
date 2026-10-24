import { Directive } from '@angular/core';

/** 44px icon target on a native element: `<button rpIconBtn aria-label="Search"><rp-icon name="search" /></button>`. */
@Directive({ selector: 'button[rpIconBtn], a[rpIconBtn]', host: { class: 'rp-icon-btn' } })
export class IconButtonDirective {}
