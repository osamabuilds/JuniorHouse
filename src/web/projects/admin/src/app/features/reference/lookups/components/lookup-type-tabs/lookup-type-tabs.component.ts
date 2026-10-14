import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { LookupTypeConfig } from '../../models';

/** The strip of lookup types; picking one is the parent's business. */
@Component({
  selector: 'app-lookup-type-tabs',
  templateUrl: './lookup-type-tabs.component.html',
  styleUrl: './lookup-type-tabs.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LookupTypeTabsComponent {
  readonly types = input.required<readonly LookupTypeConfig[]>();
  readonly selectedKey = input.required<string>();

  readonly typeSelected = output<LookupTypeConfig>();
}
