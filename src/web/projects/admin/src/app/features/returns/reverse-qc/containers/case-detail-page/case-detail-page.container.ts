import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, effect, inject, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Store } from '@ngrx/store';
import { ReturnDecision, decisionWord, statusWord } from '../../models';
import { CaseDetailActions, selectBusy, selectDetail, selectError, selectLoading, selectNotice } from '../../store';

/**
 * One returned parcel (docs/specs/returns/reverse-qc): receive it with the seal and the bin (RQC-2, RQC-3), decide each line (RQC-4 to RQC-6), and
 * complete it once every line is decided (RQC-7, RQC-8). The server enforces every rule; this page only shows what the case allows next.
 */
@Component({
  selector: 'app-return-case-detail-page',
  imports: [DatePipe, RouterLink],
  templateUrl: './case-detail-page.container.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CaseDetailPageContainer {
  private readonly store = inject(Store);

  /** From the address (`/returns/:id`), bound by the router. */
  readonly id = input.required<string>();

  readonly detail = this.store.selectSignal(selectDetail);
  readonly loading = this.store.selectSignal(selectLoading);
  readonly busy = this.store.selectSignal(selectBusy);
  readonly error = this.store.selectSignal(selectError);
  readonly notice = this.store.selectSignal(selectNotice);

  readonly status = statusWord;
  readonly decisionWord = decisionWord;
  readonly waiting = computed(() => this.detail()?.lines.filter((l) => l.decision === null).length ?? 0);

  constructor() {
    effect(() => {
      const id = Number(this.id());
      if (Number.isInteger(id) && id > 0) {
        this.store.dispatch(CaseDetailActions.opened({ id }));
      }
    });
  }

  receive(event: Event): void {
    event.preventDefault();
    const form = new FormData(event.target as HTMLFormElement);
    const binCode = String(form.get('bin') ?? '').trim();
    if (binCode.length > 0) {
      this.store.dispatch(CaseDetailActions.receiveSubmitted({ id: Number(this.id()), request: { sealIntact: form.get('seal') === 'on', binCode } }));
    }
  }

  decide(lineId: number, event: Event): void {
    event.preventDefault();
    const form = new FormData(event.target as HTMLFormElement);
    const note = String(form.get('note') ?? '').trim();
    this.store.dispatch(
      CaseDetailActions.lineDecided({
        id: Number(this.id()),
        lineId,
        request: { decision: String(form.get('decision')) as ReturnDecision, quantity: Number(form.get('quantity')), note: note.length > 0 ? note : null },
      }),
    );
  }

  complete(): void {
    this.store.dispatch(CaseDetailActions.completeSubmitted({ id: Number(this.id()) }));
  }

  dismissNotice(): void {
    this.store.dispatch(CaseDetailActions.noticeDismissed());
  }
}
