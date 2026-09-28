import { Component, computed, inject, input } from '@angular/core';
import { LookupDto } from '../reference-data/reference-api.service';
import { AppDatePipe, formatDate } from '../shared/app-date.pipe';
import { LookupNames } from '../shared/lookup-names';
import { COMM_TYPE_LABELS, INITIATOR_LABELS, PoRevisionDto, REVISION_STATUS_LABELS } from './po-api.service';

interface DiffRow {
  readonly label: string;
  readonly before: string;
  readonly after: string;
}

interface RevisionView {
  readonly revision: PoRevisionDto;
  readonly changes: readonly DiffRow[];
}

const show = (value: string | number | null | undefined, suffix = ''): string =>
  value === null || value === undefined || value === '' ? '—' : `${value}${suffix}`;

const totalQty = (revision: PoRevisionDto): number => revision.lines.reduce((sum, line) => sum + line.qty, 0);

/**
 * SCRUM-93 task 47 (AC-34): every revision of a PO with what changed against the one before it,
 * the impact figures computed when it was created, why it happened, and how the vendor's side was
 * communicated. Staff-only: the internal impact note is shown here and never in the vendor view.
 */
@Component({
  selector: 'app-revision-history',
  imports: [AppDatePipe],
  template: `
    <section class="revisions" aria-labelledby="revision-history-heading">
      <h3 id="revision-history-heading">Revision history</h3>
      @if (views().length === 0) {
        <p class="state-message">No revisions yet. Revision 0 is captured when the PO is sent.</p>
      } @else {
        <ol class="revisions__list">
          @for (view of views(); track view.revision.id) {
            <li>
              <article class="revision" [attr.aria-label]="'Revision ' + view.revision.revisionNumber">
                <h4>
                  Rev {{ view.revision.revisionNumber }}
                  <span class="badge" [class.badge--muted]="view.revision.statusId !== 2">{{ statusLabel(view.revision.statusId) }}</span>
                </h4>
                <p class="revision__meta">
                  Proposed by {{ initiatorLabel(view.revision.initiatorId) }} &middot; Reason: {{ reasonName(view.revision.reasonId) }}
                </p>

                @if (view.changes.length > 0) {
                  <table>
                    <caption class="sr-only">Changes in revision {{ view.revision.revisionNumber }}</caption>
                    <thead>
                      <tr>
                        <th scope="col">Term</th>
                        <th scope="col">Before</th>
                        <th scope="col">After</th>
                      </tr>
                    </thead>
                    <tbody>
                      @for (row of view.changes; track row.label) {
                        <tr>
                          <th scope="row">{{ row.label }}</th>
                          <td>{{ row.before }}</td>
                          <td>{{ row.after }}</td>
                        </tr>
                      }
                    </tbody>
                  </table>
                }

                @if (view.revision.revisionNumber > 0) {
                  <dl class="revision__impact">
                    <div><dt>PO value</dt><dd>PKR {{ view.revision.poValueBefore }} &rarr; PKR {{ view.revision.poValueAfter }} ({{ signed(view.revision.poValueDiff) }})</dd></div>
                    <div><dt>Advance amount</dt><dd>PKR {{ view.revision.advanceAmountBefore }} &rarr; PKR {{ view.revision.advanceAmountAfter }}</dd></div>
                    <div><dt>Delivery date shift</dt><dd>{{ signed(view.revision.expectedDateShiftDays) }} days</dd></div>
                    <div><dt>Quantity change</dt><dd>{{ signed(view.revision.quantityDiff) }} pcs</dd></div>
                  </dl>
                  @if (view.revision.isBeyondLatestAcceptableDate) {
                    <p class="banner banner--error" role="note">Expected delivery is beyond the latest acceptable date.</p>
                  }
                  <p><strong>Internal note:</strong> {{ view.revision.impactNote }}</p>
                  @if (view.revision.vendorMessage) {
                    <p><strong>Message to vendor:</strong> {{ view.revision.vendorMessage }}</p>
                  }
                }

                @if (view.revision.communications?.length) {
                  <ul class="revision__comms">
                    @for (comm of view.revision.communications; track comm.responseDte + comm.typeId) {
                      <li>
                        @if (isUnrecorded(comm.channelId)) {
                          {{ commTypeLabel(comm.typeId) }} on {{ comm.responseDte | appDate: "datetime" }} (how and by whom was not recorded).
                        } @else {
                          {{ commTypeLabel(comm.typeId) }}: {{ comm.responderName }} told us via {{ channelName(comm.channelId) }} on {{ comm.responseDte | appDate: "datetime" }}.
                        }
                        @for (file of comm.evidence ?? []; track file.fileId) {
                          <a [href]="downloadUrl()(file.fileId)">Evidence: {{ file.fileName }}</a>
                        }
                      </li>
                    }
                  </ul>
                }
              </article>
            </li>
          }
        </ol>
      }
    </section>
  `,
  styles: `
    .revisions__list {
      list-style: none;
      margin: 0 0 var(--space-5);
      padding: 0;
      display: grid;
      gap: var(--space-4);
    }
    .revision {
      border: 1px solid var(--color-border);
      border-radius: var(--radius-md);
      padding: var(--space-4);
    }
    .revision h4 {
      margin: 0 0 var(--space-2);
      display: flex;
      align-items: center;
      gap: var(--space-2);
    }
    .revision__meta {
      color: var(--color-ink-soft);
      margin: 0 0 var(--space-3);
    }
    .revision__impact {
      display: grid;
      gap: var(--space-2);
      margin: var(--space-3) 0;
    }
    .revision__impact div {
      display: flex;
      justify-content: space-between;
      border-bottom: 1px solid var(--color-border);
      padding-bottom: var(--space-1);
    }
    .revision__impact dt {
      color: var(--color-ink-soft);
    }
    .revision__impact dd {
      margin: 0;
    }
    .revision__comms {
      margin: var(--space-3) 0 0;
      padding-left: var(--space-5);
    }
  `,
})
export class RevisionHistory {
  private readonly names = inject(LookupNames);
  readonly revisions = input.required<readonly PoRevisionDto[]>();
  readonly reasons = input<readonly LookupDto[]>([]);
  readonly channels = input<readonly LookupDto[]>([]);
  readonly fabricOptions = input<readonly LookupDto[]>([]);
  /** Builds the download link for an evidence file; supplied by the parent, which knows the PO. */
  readonly downloadUrl = input<(fileId: number) => string>(() => '#');

  readonly views = computed<RevisionView[]>(() => {
    const ordered = [...this.revisions()].sort((a, b) => a.revisionNumber - b.revisionNumber);
    return ordered.map((revision, index) => ({
      revision,
      changes: index === 0 ? [] : this.diff(ordered[index - 1], revision),
    }));
  });

  statusLabel(statusId: number): string {
    return REVISION_STATUS_LABELS[statusId] ?? `#${statusId}`;
  }

  initiatorLabel(initiatorId: number): string {
    return INITIATOR_LABELS[initiatorId] ?? `#${initiatorId}`;
  }

  commTypeLabel(typeId: number): string {
    return COMM_TYPE_LABELS[typeId] ?? `#${typeId}`;
  }

  reasonName(reasonId: number): string {
    return this.reasons().find((reason) => reason.id === reasonId)?.name ?? `#${reasonId}`;
  }

  /** Records made before channels were captured (or by the old Acknowledge button) carry the Unspecified channel. */
  isUnrecorded(channelId: number): boolean {
    return this.channelName(channelId) === 'Unspecified';
  }

  channelName(channelId: number): string {
    return this.channels().find((channel) => channel.id === channelId)?.name ?? `#${channelId}`;
  }

  signed(value: number): string {
    return value > 0 ? `+${value}` : `${value}`;
  }

  private fabricName(id: number | null): string {
    return id === null ? '—' : (this.fabricOptions().find((option) => option.id === id)?.name ?? `#${id}`);
  }

  private diff(before: PoRevisionDto, after: PoRevisionDto): DiffRow[] {
    const rows: DiffRow[] = [];
    const add = (label: string, b: string, a: string): void => {
      if (b !== a) {
        rows.push({ label, before: b, after: a });
      }
    };

    add('Unit cost', show(before.unitCost, ' PKR'), show(after.unitCost, ' PKR'));
    add('Expected delivery', formatDate(before.expectedDeliveryDate), formatDate(after.expectedDeliveryDate));
    add('Latest acceptable delivery', formatDate(before.latestAcceptableDate), formatDate(after.latestAcceptableDate));
    add('Extra pieces allowed', show(before.overTolerancePercent, '%'), show(after.overTolerancePercent, '%'));
    add('Fewer pieces allowed', show(before.underTolerancePercent, '%'), show(after.underTolerancePercent, '%'));
    add('Advance payment', show(before.advancePercent, '%'), show(after.advancePercent, '%'));
    add('Payment terms', this.names.paymentTermName(before.paymentTermId), this.names.paymentTermName(after.paymentTermId));
    add('Who supplies the fabric', this.fabricName(before.fabricResponsibilityId), this.fabricName(after.fabricResponsibilityId));
    add('Total quantity', show(totalQty(before), ' pcs'), show(totalQty(after), ' pcs'));

    const cell = (line: { sizeId: number; colourId: number }): string => `${line.sizeId}-${line.colourId}`;
    const beforeLines = new Map(before.lines.map((line) => [cell(line), line.qty]));
    const afterLines = new Map(after.lines.map((line) => [cell(line), line.qty]));
    for (const key of new Set([...beforeLines.keys(), ...afterLines.keys()])) {
      const [size, colour] = key.split('-');
      add(`${this.names.sizeName(+size)}, ${this.names.colourName(+colour)}`, show(beforeLines.get(key) ?? 0, ' pcs'), show(afterLines.get(key) ?? 0, ' pcs'));
    }

    return rows;
  }
}
