import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { LookupDto } from '@features/reference-data';
import { StyleSummaryDto } from '../../models';

@Component({
  selector: 'app-style-table',
  templateUrl: './style-table.component.html',
  styles: ':host { display: block; }',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StyleTableComponent {
  readonly styles = input.required<readonly StyleSummaryDto[]>();
  readonly loading = input.required<boolean>();
  readonly categories = input.required<readonly LookupDto[]>();

  readonly edit = output<StyleSummaryDto>();

  categoryName(categoryId: number): string {
    return this.categories().find((category) => category.id === categoryId)?.name ?? `#${categoryId}`;
  }
}
