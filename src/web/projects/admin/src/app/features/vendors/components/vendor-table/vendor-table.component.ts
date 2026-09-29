import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { LookupDto } from '@features/reference-data';
import { VendorSummaryDto } from '../../models';

@Component({
  selector: 'app-vendor-table',
  templateUrl: './vendor-table.component.html',
  styles: ':host { display: block; }',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class VendorTableComponent {
  readonly vendors = input.required<readonly VendorSummaryDto[]>();
  readonly loading = input.required<boolean>();
  readonly cities = input.required<readonly LookupDto[]>();

  readonly edit = output<VendorSummaryDto>();

  cityName(cityId: number): string {
    return this.cities().find((city) => city.id === cityId)?.name ?? `#${cityId}`;
  }
}
