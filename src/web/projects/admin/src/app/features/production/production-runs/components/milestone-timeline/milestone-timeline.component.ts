import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { LookupNameResolver } from '@features/reference/lookups';
import { AppDatePipe } from '@shared';
import { MilestoneDto } from '../../models';
import { MILESTONE_LABELS } from '../../production-runs.constants';

/** The milestone timeline of a run (spec AC-5, AC-7, AC-36, AC-38): what happened, on which day, who reported it and who recorded it. */
@Component({
  selector: 'app-milestone-timeline',
  imports: [AppDatePipe],
  template: `
    @if (milestones().length === 0) {
      <p class="state-message state-message--inline">Nothing recorded yet. Record each step as the vendor reports it.</p>
    } @else {
      <ol class="timeline">
        @for (milestone of milestones(); track milestone.id) {
          <li>
            <span class="timeline__status">{{ labels[milestone.typeId] }}</span>
            <span class="timeline__meta">
              {{ milestone.claimedDate | appDate }} &middot; reported by {{ milestone.reportedBy }} &middot; recorded by
              {{ milestone.actingPerson }}
            </span>
            @if (milestone.isRecordedLate) {
              <span class="badge badge--muted">Recorded late</span>
            }
            @if (milestone.flagReason) {
              <span class="timeline__warning" role="note"><strong>Warning:</strong> {{ milestone.flagReason }}</span>
            }
            @if (milestone.note) {
              <span class="timeline__reason">{{ milestone.note }}</span>
            }
            @if (milestone.finishedLines.length > 0) {
              <span class="timeline__reason">
                Ready to ship:
                @for (line of milestone.finishedLines; track line.sizeId + '-' + line.colourId; let last = $last) {
                  {{ line.qty }} × {{ names().sizeName(line.sizeId) }} / {{ names().colourName(line.colourId) }}{{ last ? '' : ',' }}
                }
              </span>
            }
          </li>
        }
      </ol>
    }
  `,
  styles: `
    :host {
      display: block;
    }
    .timeline__warning {
      display: block;
      margin-top: var(--space-1);
      padding: var(--space-2) var(--space-3);
      border-radius: var(--radius-sm);
      background: var(--color-danger-tint);
      color: var(--color-danger);
      font-size: var(--text-sm);
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MilestoneTimelineComponent {
  readonly milestones = input.required<readonly MilestoneDto[]>();
  readonly names = input.required<LookupNameResolver>();

  readonly labels = MILESTONE_LABELS;
}
