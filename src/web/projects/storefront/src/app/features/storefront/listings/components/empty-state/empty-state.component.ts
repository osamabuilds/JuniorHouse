import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Suggestion } from '../../models/listing.model';

/** What a visitor sees when nothing is listed: why, and where to go next (LST-5, LST-6). Never a dead end. */
@Component({
  selector: 'rp-empty-state',
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './empty-state.component.html',
  styleUrl: './empty-state.component.scss',
})
export class EmptyStateComponent {
  /** True when filters are active, so the message is about them rather than about the shop. */
  readonly filtered = input.required<boolean>();
  readonly suggestions = input<readonly Suggestion[]>([]);
  /** Words that replace the default heading and message (for example "Nothing found for ..." on a search). */
  readonly heading = input<string | null>(null);
  readonly message = input<string | null>(null);
}
