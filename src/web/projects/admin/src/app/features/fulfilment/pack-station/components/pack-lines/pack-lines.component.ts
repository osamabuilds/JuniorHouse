import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { PackLine } from '../../models';

/** What to scan at the pack station and how many of each are in the parcel so far (PAK-1). Presentational. */
@Component({
  selector: 'app-pack-lines',
  templateUrl: './pack-lines.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PackLinesComponent {
  readonly lines = input.required<readonly PackLine[]>();

  isDone(line: PackLine): boolean {
    return line.packed >= line.quantity;
  }
}
