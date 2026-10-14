import { ChangeDetectionStrategy, Component, input, output, signal } from '@angular/core';
import { GateFailureDto, PRODUCT_TABS, ProductDto, ProductTab } from '../../models';

/** "NO_SIZE_CHART" and friends, in words. */
const WARNINGS: Readonly<Record<string, string>> = {
  NO_SIZE_CHART: 'There is no size chart for this category yet. The product can still be published; the shop will say "size chart coming soon".',
};

/**
 * The Publish tab (PRD-3, PRD-4, PRD-6): what is still missing before the product can go on sale, each item a real link to the tab that
 * fixes it, and the publish, unpublish and archive buttons. Archive asks for a second click because it cannot be undone.
 */
@Component({
  selector: 'app-publish-panel',
  template: `
    <p>
      Status: <span class="badge" [class.badge--muted]="product().status !== 'Published'">{{ product().status }}</span>
      @if (product().status === 'Published') {
        <span>Live at <code>{{ product().path }}</code></span>
      }
    </p>
    <p>{{ product().sellableVariants }} of {{ product().variants.length }} variants can be bought now. The others read "Not yet available" until Labelling issues their barcode.</p>

    @if (failures().length > 0) {
      <section class="banner banner--error" role="alert" aria-labelledby="gate-heading">
        <h3 id="gate-heading">Not ready to publish</h3>
        <ul>
          @for (failure of failures(); track failure.code + failure.message) {
            <li>
              {{ failure.message }}
              <a [href]="'#' + failure.tab" (click)="$event.preventDefault(); tabSelected.emit(failure.tab)">Go to {{ tabLabel(failure.tab) }}</a>
            </li>
          }
        </ul>
      </section>
    } @else if (product().status === 'Draft' || product().status === 'Unpublished') {
      <p class="banner banner--success" role="status">Everything needed is in place. The product can be published.</p>
    }

    @for (warning of product().gate.warnings; track warning) {
      <p class="banner banner--info" role="status">{{ warningText(warning) }}</p>
    }

    <div class="form-actions">
      @if (product().status === 'Published') {
        <button type="button" class="button" [disabled]="saving()" (click)="unpublish.emit()">Take off sale</button>
      } @else if (product().status !== 'Archived') {
        <button type="button" class="button button--primary" [disabled]="saving()" (click)="publish.emit()">Publish</button>
      }
      @if (product().status !== 'Archived') {
        @if (confirmingArchive()) {
          <button type="button" class="button button--danger" [disabled]="saving()" (click)="confirmingArchive.set(false); archive.emit()">Yes, archive it for good</button>
          <button type="button" class="button" (click)="confirmingArchive.set(false)">Keep it</button>
        } @else {
          <button type="button" class="button" [disabled]="saving()" (click)="confirmingArchive.set(true)">Archive</button>
        }
      } @else {
        <p class="state-message">This product is archived. It is kept for order history and cannot be edited or published.</p>
      }
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PublishPanelComponent {
  readonly product = input.required<ProductDto>();
  /** The failures of the last refused publish, or the gate preview when none has been tried. */
  readonly failures = input<readonly GateFailureDto[]>([]);
  readonly saving = input(false);

  readonly publish = output<void>();
  readonly unpublish = output<void>();
  readonly archive = output<void>();
  readonly tabSelected = output<ProductTab>();

  readonly confirmingArchive = signal(false);

  tabLabel(tab: ProductTab): string {
    return PRODUCT_TABS.find((candidate) => candidate.key === tab)?.label ?? tab;
  }

  warningText(code: string): string {
    return WARNINGS[code] ?? code;
  }
}
