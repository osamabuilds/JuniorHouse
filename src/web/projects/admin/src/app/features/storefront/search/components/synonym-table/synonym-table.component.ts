import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { SynonymGroup } from '../../models';

@Component({
  selector: 'app-synonym-table',
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: ':host { display: block; }',
  template: `
    @if (loading()) {
      <p class="state-message">Loading…</p>
    } @else if (groups().length === 0) {
      <p class="state-message">No synonym groups yet.</p>
    } @else {
      <table class="responsive">
        <caption class="sr-only">Synonym groups</caption>
        <thead>
          <tr>
            <th scope="col">Name</th>
            <th scope="col">Words</th>
            <th scope="col">Status</th>
            <th scope="col">Actions</th>
          </tr>
        </thead>
        <tbody>
          @for (group of groups(); track group.id) {
            <tr>
              <th scope="row" data-label="Name">{{ group.name }}</th>
              <td data-label="Words">{{ group.terms.join(', ') }}</td>
              <td data-label="Status">
                <span class="badge" [class.badge--muted]="!group.isActive">{{ group.isActive ? 'In use' : 'Switched off' }}</span>
              </td>
              <td data-label="Actions">
                @if (deletingId() === group.id) {
                  <button type="button" class="button button--small button--danger" (click)="deleteConfirmed.emit(group.id)">
                    Delete {{ group.name }}
                  </button>
                  <button type="button" class="button button--small" (click)="deleteCancelled.emit()">Keep</button>
                } @else {
                  <button type="button" class="button button--small" (click)="edit.emit(group)">
                    Edit<span class="sr-only"> {{ group.name }}</span>
                  </button>
                  <button type="button" class="button button--small" (click)="deleteRequested.emit(group.id)">
                    Delete<span class="sr-only"> {{ group.name }}</span>
                  </button>
                }
              </td>
            </tr>
          }
        </tbody>
      </table>
    }
  `,
})
export class SynonymTableComponent {
  readonly groups = input.required<readonly SynonymGroup[]>();
  readonly loading = input.required<boolean>();
  readonly deletingId = input<number | null>(null);

  readonly edit = output<SynonymGroup>();
  readonly deleteRequested = output<number>();
  readonly deleteCancelled = output<void>();
  readonly deleteConfirmed = output<number>();
}
