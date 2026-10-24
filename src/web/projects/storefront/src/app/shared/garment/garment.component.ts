import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { GarmentShape } from '../data/mock-catalog';

/** Flat-garment stand-in drawn inside an `rp-ph` placeholder; deleted with the placeholders when photography exists. */
@Component({
  selector: 'rp-garment',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { style: 'display: contents' },
  template: `
    <svg class="rp-garment" viewBox="0 0 100 100" aria-hidden="true">
      @switch (shape()) {
        @case ('dress') {
          <path d="M38 8h24l3 22 18 62H17l18-62z" class="gf" />
          <path d="M36 30h28M44 34c-2 18-6 36-10 54M56 34c2 18 6 36 10 54" fill="none" stroke="rgba(255,252,246,0.45)" stroke-width="0.8" stroke-linecap="round" />
        }
        @case ('shirt') {
          <path d="M34 10c6 6 26 6 32 0l22 12-8 22-10-4v50H30V40l-10 4-8-22z" class="gf" />
          <path d="M50 16v78M36 14c4 3 8 5 14 5s10-2 14-5" fill="none" stroke="rgba(255,252,246,0.45)" stroke-width="0.8" stroke-linecap="round" />
        }
        @case ('trouser') {
          <path d="M28 8h44l6 90H55l-5-60-5 60H22z" class="gf" />
          <path d="M28 16h44M50 38V16" fill="none" stroke="rgba(255,252,246,0.45)" stroke-width="0.8" stroke-linecap="round" />
        }
        @case ('hat') {
          <ellipse cx="50" cy="62" rx="44" ry="9" class="gf" />
          <path d="M22 60c0-26 14-38 28-38s28 12 28 38z" class="gf2" />
          <path d="M24 54h52" fill="none" stroke="rgba(255,252,246,0.45)" stroke-width="0.8" stroke-linecap="round" />
        }
      }
    </svg>
  `,
})
export class GarmentComponent {
  readonly shape = input.required<GarmentShape>();
}
