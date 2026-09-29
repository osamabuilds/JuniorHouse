import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ApiError } from '@core/http';
import { LookupDto, LookupNameResolver } from '@features/reference-data';
import { VendorSummaryDto } from '@features/vendors';
import { AppDatePipe, selectNumberOrNull } from '@shared';
import {
  AmendmentValue,
  PoDto,
  PoFileDto,
  PoFileUpload,
  PoPanel,
  PoRevisionDto,
  StyleCell,
  VendorResponseValue,
} from '../../models';
import { PO_STATUS_DRAFT, PO_STATUS_LABELS } from '../../purchase-orders.constants';
import { canAmend, canCancel, canEdit, canRecordResponse, canSend, statusHelp } from '../../utils/po-status-rules.util';
import { AmendFormComponent } from '../amend-form/amend-form.component';
import { PendingRevisionActionsComponent } from '../pending-revision-actions/pending-revision-actions.component';
import { PoFilesPanelComponent } from '../po-files-panel/po-files-panel.component';
import { RevisionHistoryComponent } from '../revision-history/revision-history.component';
import { SendConfirmComponent } from '../send-confirm/send-confirm.component';
import { VendorResponseFormComponent } from '../vendor-response-form/vendor-response-form.component';

export interface RevisionNoteDecision {
  readonly revision: PoRevisionDto;
  readonly note: string | null;
}

/**
 * SCRUM-174: a PO's detail view with the status timeline (AC-14) and the Send / Record response /
 * Amend / Cancel actions, each shown only when legal for the PO's current status. It holds no
 * state and calls nothing: everything the user does is emitted for the container to act on.
 */
@Component({
  selector: 'app-po-detail',
  imports: [
    RouterLink,
    AppDatePipe,
    AmendFormComponent,
    PendingRevisionActionsComponent,
    PoFilesPanelComponent,
    RevisionHistoryComponent,
    SendConfirmComponent,
    VendorResponseFormComponent,
  ],
  templateUrl: './po-detail.component.html',
  styleUrl: './po-detail.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PoDetailComponent {
  readonly po = input.required<PoDto>();
  readonly names = input.required<LookupNameResolver>();
  readonly vendors = input.required<readonly VendorSummaryDto[]>();
  readonly error = input.required<ApiError | null>();

  readonly panel = input.required<PoPanel>();
  readonly panelSaving = input.required<boolean>();
  readonly panelError = input.required<ApiError | null>();

  readonly hasTechPack = input.required<boolean>();
  readonly currentRevisionNumber = input.required<number>();
  readonly pendingRevision = input.required<PoRevisionDto | null>();
  readonly revisions = input.required<readonly PoRevisionDto[]>();
  readonly files = input.required<readonly PoFileDto[]>();
  readonly styleCells = input.required<readonly StyleCell[]>();

  readonly channels = input.required<readonly LookupDto[]>();
  readonly amendmentReasons = input.required<readonly LookupDto[]>();
  readonly fabricOptions = input.required<readonly LookupDto[]>();
  readonly cancelReasons = input.required<readonly LookupDto[]>();
  readonly cancelReasonId = input.required<number | null>();

  readonly fileUploading = input.required<boolean>();
  readonly fileError = input.required<ApiError | null>();
  /** Builds a file's download link; the container knows the API's address. */
  readonly fileUrlFor = input.required<(fileId: number) => string>();

  readonly editRequested = output<void>();
  readonly panelOpened = output<Exclude<PoPanel, 'none'>>();
  readonly panelClosed = output<void>();
  readonly sendConfirmed = output<boolean>();
  readonly amended = output<AmendmentValue>();
  readonly vendorResponseRecorded = output<VendorResponseValue>();
  readonly revisionAccepted = output<PoRevisionDto>();
  readonly revisionRejected = output<RevisionNoteDecision>();
  readonly revisionWithdrawn = output<RevisionNoteDecision>();
  readonly fileUploadRequested = output<PoFileUpload>();
  readonly fileRemoveRequested = output<PoFileDto>();
  readonly cancelReasonChanged = output<number | null>();
  readonly cancelRequested = output<void>();
  readonly backRequested = output<void>();

  readonly statusLabels = PO_STATUS_LABELS;
  readonly draftStatusId = PO_STATUS_DRAFT;

  readonly canEdit = canEdit;
  readonly canSend = canSend;
  readonly canRecordResponse = canRecordResponse;
  readonly canAmend = canAmend;
  readonly canCancel = canCancel;
  readonly statusHelp = statusHelp;

  onCancelReasonChange(event: Event): void {
    this.cancelReasonChanged.emit(selectNumberOrNull(event));
  }

  /** Download link for an evidence file behind a vendor communication on the open PO. */
  readonly evidenceUrl = (fileId: number): string => this.fileUrlFor()(fileId);

  fabricName(id: number | null): string {
    return id === null ? '—' : (this.fabricOptions().find((option) => option.id === id)?.name ?? `#${id}`);
  }

  vendorName(vendorId: number): string {
    return this.vendors().find((vendor) => vendor.id === vendorId)?.name ?? `#${vendorId}`;
  }

  cancelReasonName(cancelReasonId: number | null): string {
    if (cancelReasonId === null) {
      return '—';
    }
    return this.cancelReasons().find((reason) => reason.id === cancelReasonId)?.name ?? `#${cancelReasonId}`;
  }
}
