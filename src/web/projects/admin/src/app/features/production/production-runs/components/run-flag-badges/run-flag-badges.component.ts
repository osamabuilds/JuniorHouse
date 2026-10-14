import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { RunFlag } from '../../utils';

/**
 * Warnings about a run, always as words (spec AC-33: text as well as colour, so nothing depends on
 * seeing red). Holds no state; the flags are worked out by `runFlags`.
 */
@Component({
  selector: 'app-run-flag-badges',
  template: `
    @if (flags().length > 0) {
      <ul class="flags" aria-label="Warnings">
        @for (flag of flags(); track flag.code) {
          <li>
            <span class="flag" [class.flag--danger]="flag.tone === 'danger'" [class.flag--warn]="flag.tone === 'warn'">
              <span aria-hidden="true">{{ flag.tone === 'danger' ? '⚠' : '•' }}</span>
              {{ flag.label }}
            </span>
          </li>
        }
      </ul>
    }
  `,
  styles: `
    :host {
      display: block;
    }
    .flags {
      list-style: none;
      display: flex;
      flex-wrap: wrap;
      gap: var(--space-2);
      margin: 0;
      padding: 0;
    }
    .flag {
      display: inline-block;
      padding: 0.1875rem var(--space-2);
      border-radius: var(--radius-sm);
      font-size: var(--text-xs);
      font-weight: 600;
      background: var(--color-brass-tint);
      color: var(--color-ink);
      border: 1px solid var(--color-border-strong);
    }
    .flag--danger {
      background: var(--color-danger-tint);
      color: var(--color-danger);
      border-color: var(--color-danger);
    }
    .flag--warn {
      background: var(--color-brass-tint);
      color: var(--color-brass-dark);
      border-color: var(--color-brass);
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RunFlagBadgesComponent {
  readonly flags = input.required<readonly RunFlag[]>();
}
