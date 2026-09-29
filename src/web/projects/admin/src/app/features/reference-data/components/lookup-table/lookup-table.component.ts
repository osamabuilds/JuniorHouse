import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { LookupDto } from '../../models';

@Component({
  selector: 'app-lookup-table',
  templateUrl: './lookup-table.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LookupTableComponent {
  readonly items = input.required<readonly LookupDto[]>();
  readonly loading = input.required<boolean>();
  readonly caption = input.required<string>();
  readonly mutable = input.required<boolean>();

  readonly edit = output<LookupDto>();
  readonly retire = output<LookupDto>();
}
