import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { AppDatePipe } from '@shared';
import { SnapshotRow } from '../../models';

/** The published versions, newest first. The one on the shop is marked in words; any other can be restored (ST-12). */
@Component({
  selector: 'app-snapshots-table',
  imports: [AppDatePipe],
  template: `
    @if (loading()) {
      <p class="state-message" role="status">Loading the versions…</p>
    } @else if (snapshots().length === 0) {
      <p class="state-message">Nothing has been published yet.</p>
    } @else {
      <div class="table-scroll">
        <table>
          <caption class="sr-only">Published versions</caption>
          <thead>
            <tr>
              <th scope="col">Version</th>
              <th scope="col">Published</th>
              <th scope="col">By</th>
              <th scope="col">Note</th>
              <th scope="col"><span class="sr-only">Actions</span></th>
            </tr>
          </thead>
          <tbody>
            @for (snapshot of snapshots(); track snapshot.versionNo) {
              <tr>
                <th scope="row">
                  Version {{ snapshot.versionNo }}
                  @if (snapshot.isCurrent) {
                    <span class="badge">on the shop</span>
                  }
                </th>
                <td>{{ snapshot.publishedAt | appDate: 'datetime' }}</td>
                <td>{{ snapshot.publishedBy }}</td>
                <td>{{ snapshot.note || '—' }}</td>
                <td>
                  @if (!snapshot.isCurrent) {
                    <button
                      type="button"
                      class="button button--small"
                      [disabled]="restoringVersion() !== null"
                      [attr.aria-label]="'Restore version ' + snapshot.versionNo"
                      (click)="restore.emit(snapshot.versionNo)"
                    >
                      {{ restoringVersion() === snapshot.versionNo ? 'Restoring…' : 'Restore' }}
                    </button>
                  }
                </td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    }
  `,
  styles: '.table-scroll { overflow-x: auto; }',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SnapshotsTableComponent {
  readonly snapshots = input.required<readonly SnapshotRow[]>();
  readonly loading = input(false);
  readonly restoringVersion = input<number | null>(null);

  readonly restore = output<number>();
}
