import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { Meta, Title } from '@angular/platform-browser';
import { ImageAsset } from '../shared/models/image-asset.model';
import { ButtonDirective } from '../shared/ui/button/button.directive';
import { FieldComponent } from '../shared/ui/field/field.component';
import { IconButtonDirective } from '../shared/ui/icon-button/icon-button.directive';
import { IconComponent, RpIconName } from '../shared/ui/icon/icon.component';
import { RP_IMAGE_SIZES } from '../shared/ui/image/image-loader';
import { ImageComponent } from '../shared/ui/image/image.component';
import { LinkCtaDirective } from '../shared/ui/link-cta/link-cta.directive';

const photo = (name: string, alt: string): ImageAsset => ({
  src: `/placeholders/${name}.svg`,
  width: 1080,
  height: 1350,
  alt,
});

/** Development-only visual check of the design foundation. Not linked, not indexed. */
@Component({
  selector: 'rp-foundations',
  imports: [ButtonDirective, LinkCtaDirective, IconButtonDirective, IconComponent, FieldComponent, ImageComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './foundations.page.scss',
  template: `
    <header class="rp-wrap rp-section--tight">
      <p class="t-eyebrow">Design foundation</p>
    </header>
    <main class="rp-wrap">
      <h1 class="rp-display-lg">Dressed for <em>the day</em></h1>
      <p class="t-body-lg lead">Tokens, type, buttons, fields, icons and images, in one place.</p>

      <section class="rp-section" aria-labelledby="type-h">
        <h2 id="type-h" class="rp-display-md">Type</h2>
        <p class="rp-display-sm">Display small, Instrument Serif</p>
        <p class="t-body">Body, DM Sans. Calm, specific, understated.</p>
        <p class="t-caption">Caption, 12 pixels</p>
        <p class="t-eyebrow">Eyebrow</p>
      </section>

      <section class="rp-section" aria-labelledby="btn-h">
        <h2 id="btn-h" class="rp-display-md">Buttons</h2>
        <div class="row">
          <button rpBtn type="button">Add to bag</button>
          <button rpBtn="ghost" type="button">Show more</button>
          <button rpBtn size="sm" type="button">Small</button>
          <button rpBtn type="button" disabled reasonId="size-reason">Add to bag</button>
          <a rpLink href="/foundations-link">Shop girls <rp-icon name="arrow" /></a>
          <button rpIconBtn type="button" aria-label="Search"><rp-icon name="search" /></button>
          <button rpIconBtn type="button" aria-label="Bag"><rp-icon name="bag" /></button>
        </div>
        <p id="size-reason" class="t-caption">Select a size</p>
      </section>

      <section class="rp-section" aria-labelledby="field-h">
        <h2 id="field-h" class="rp-display-md">Fields</h2>
        <div class="fields">
          <rp-field #a label="Mobile number" hint="For delivery updates">
            <input class="rp-input" type="tel" autocomplete="tel" [id]="a.controlId" [attr.aria-describedby]="a.describedBy()" />
          </rp-field>
          <rp-field #b label="Email" error="Enter an address like name@example.com">
            <input class="rp-input" type="email" autocomplete="email" [id]="b.controlId" [attr.aria-describedby]="b.describedBy()" [attr.aria-invalid]="b.invalid()" />
          </rp-field>
        </div>
      </section>

      <section class="rp-section" aria-labelledby="img-h">
        <h2 id="img-h" class="rp-display-md">Images</h2>
        <ul class="grid">
          <li>
            <rp-image [image]="front" [second]="fabric" [priority]="true" [sizes]="sizes" />
          </li>
          <li><rp-image [image]="back" [sizes]="sizes" /></li>
          <li><rp-image [image]="null" tone="clay" [sizes]="sizes" /></li>
          <li><div class="rp-media rp-skel" aria-hidden="true"></div></li>
        </ul>
      </section>

      <section class="rp-section" aria-labelledby="ico-h">
        <h2 id="ico-h" class="rp-display-md">Icons</h2>
        <ul class="row">
          @for (n of icons; track n) {
            <li><rp-icon [name]="n" [label]="n" /></li>
          }
        </ul>
      </section>
    </main>
    <footer class="rp-wrap rp-section--tight">
      <p class="t-caption">Placeholders only. No brand assets.</p>
    </footer>
  `,
})
export class FoundationsPage {
  protected readonly front = photo('front', 'Placeholder garment, front view');
  protected readonly back = photo('back', 'Placeholder garment, back view');
  protected readonly fabric = photo('fabric', 'Placeholder fabric close-up');
  protected readonly sizes = RP_IMAGE_SIZES.card;
  protected readonly icons: RpIconName[] = [
    'search', 'heart', 'bag', 'menu', 'close', 'arrow', 'arrow-left',
    'check', 'alert', 'truck', 'swap', 'chat', 'cash', 'pin',
  ];

  constructor() {
    inject(Title).setTitle('Design foundation');
    inject(Meta).updateTag({ name: 'robots', content: 'noindex, nofollow' });
  }
}
