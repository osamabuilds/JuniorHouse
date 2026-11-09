import { ChangeDetectionStrategy, Component, effect, inject, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Store } from '@ngrx/store';
import { LabelSheetComponent } from '../../components/label-sheet/label-sheet.component';
import { LabelPageActions, selectError, selectLabel, selectLoading } from '../../store/labels.store';

/**
 * The shipping label (docs/specs/shipping/labels, LBL-1 to LBL-7): the page has one heading, the label itself prints on its own, and "Print the label"
 * uses the browser then records the print so the next one is marked a reprint. There is no label before a booking (LBL-2): the page says why.
 */
@Component({
  selector: 'app-label-page',
  imports: [RouterLink, LabelSheetComponent],
  templateUrl: './label-page.container.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LabelPageContainer {
  private readonly store = inject(Store);

  /** From the address (`/dispatch/label/:id`), bound by the router. */
  readonly id = input.required<string>();

  readonly label = this.store.selectSignal(selectLabel);
  readonly loading = this.store.selectSignal(selectLoading);
  readonly error = this.store.selectSignal(selectError);

  constructor() {
    effect(() => {
      const id = Number(this.id());
      if (Number.isInteger(id) && id > 0) {
        this.store.dispatch(LabelPageActions.opened({ id }));
      }
    });
  }

  print(): void {
    window.print();
    this.store.dispatch(LabelPageActions.printed({ id: Number(this.id()) }));
  }
}
