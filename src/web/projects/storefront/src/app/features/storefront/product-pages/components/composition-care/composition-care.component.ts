import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { CareLine, CompositionLine } from '../../models/product-page.model';

/**
 * Fibre percentages and care instructions (PDP-8, FR-09), all in the server HTML. Care is always written in words. Pictograms are not
 * shown until their artwork licence is settled (Q-PDP-3); the symbol code is kept as a hook only, so no image carries meaning.
 */
@Component({
  selector: 'rp-composition-care',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section aria-labelledby="composition-heading">
      <h2 id="composition-heading" class="rp-eyebrow part__title">Composition</h2>
      @if (composition().length > 0) {
        <dl class="fibres">
          @for (line of composition(); track line.name) {
            <div class="fibres__row">
              <dt>{{ line.name }}</dt>
              <dd>{{ line.percent }}%</dd>
            </div>
          }
        </dl>
      } @else {
        <p class="part__none">Composition is not listed for this piece.</p>
      }
    </section>

    <section aria-labelledby="care-heading">
      <h2 id="care-heading" class="rp-eyebrow part__title">Care</h2>
      @if (care().length > 0) {
        <ul class="care">
          @for (line of care(); track line.name) {
            <li [attr.data-symbol]="line.symbolCode">{{ line.name }}</li>
          }
        </ul>
      } @else {
        <p class="part__none">Care instructions are not listed for this piece.</p>
      }
      @if (note()) {
        <p class="part__note">{{ note() }}</p>
      }
    </section>
  `,
  styles: `
    :host { display: grid; gap: var(--space-6); }
    .part__title { margin: 0 0 var(--space-3); }
    .fibres { margin: 0; }
    .fibres__row { display: flex; justify-content: space-between; gap: var(--space-4); padding: var(--space-2) 0; border-bottom: 1px solid var(--line); font: 400 15px/24px var(--font-sans); }
    .fibres dt, .fibres dd { margin: 0; }
    .care { margin: 0; padding-left: var(--space-5); font: 400 15px/24px var(--font-sans); }
    .part__none, .part__note { margin: 0; font: 400 15px/24px var(--font-sans); color: var(--ink-muted); }
    .part__note { margin-top: var(--space-2); }
  `,
})
export class CompositionCareComponent {
  readonly composition = input.required<readonly CompositionLine[]>();
  readonly care = input.required<readonly CareLine[]>();
  readonly note = input<string | null>(null);
}
