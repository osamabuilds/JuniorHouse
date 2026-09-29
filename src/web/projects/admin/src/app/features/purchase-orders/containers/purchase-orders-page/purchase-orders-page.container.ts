import { ChangeDetectionStrategy, Component, computed, effect, inject, signal, untracked } from '@angular/core';
import { FormBuilder } from '@angular/forms';
import { Store } from '@ngrx/store';
import { LookupNames, ReferenceLookupActions, selectLookup } from '@features/reference-data';
import { VendorDto } from '@features/vendors';
import { missingSummary, PaginationComponent } from '@shared';
import { PoDetailComponent, RevisionNoteDecision } from '../../components/po-detail/po-detail.component';
import { PoFormComponent } from '../../components/po-form/po-form.component';
import { PoListComponent } from '../../components/po-list/po-list.component';
import { buildPoForm } from '../../forms/po.form';
import {
  AmendmentValue,
  LineQtyChange,
  LineRow,
  PoFileDto,
  PoFileUpload,
  PoPanel,
  PoRevisionDto,
  PoSummaryDto,
  VendorResponseValue,
  lineKey,
} from '../../models';
import { PoApiService } from '../../services/po-api.service';
import {
  PurchaseOrdersPageActions,
  selectCancelReasonId,
  selectCurrentPo,
  selectCurrentRevisionNumber,
  selectFileError,
  selectFileUploading,
  selectFiles,
  selectHasTechPack,
  selectOrders,
  selectOrdersPage,
  selectOrdersPageSize,
  selectOrdersTotal,
  selectOrdersError,
  selectOrdersLoading,
  selectOrdersMode,
  selectOrdersSaving,
  selectPanel,
  selectPanelError,
  selectPanelSaving,
  selectPendingRevision,
  selectRevisions,
  selectSelectedStyle,
  selectSelectedVendor,
  selectStatusFilter,
  selectStyleCells,
  selectStyleOptions,
  selectVendorFilter,
  selectVendorOptions,
} from '../../store';

const LOOKUP_TYPES = ['payment-terms', 'amendment-reasons', 'vendor-comm-channels', 'fabric-responsibilities', 'po-cancel-reasons'] as const;

/**
 * SCRUM-174: PO list (filter by vendor/status), create/edit (Draft only, AC-9/AC-13) and a
 * detail view with the status timeline (AC-14) and Send/Acknowledge/Cancel actions, each disabled
 * when illegal for the PO's current status. This container reads the store and dispatches; the
 * list, detail and form are presentational components.
 */
@Component({
  selector: 'app-purchase-orders-page',
  imports: [PoListComponent, PoDetailComponent, PoFormComponent, PaginationComponent],
  templateUrl: './purchase-orders-page.container.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PurchaseOrdersPageContainer {
  private readonly store = inject(Store);
  private readonly poApi = inject(PoApiService);
  private readonly formBuilder = inject(FormBuilder);
  readonly names = inject(LookupNames);

  readonly orders = this.store.selectSignal(selectOrders);
  readonly total = this.store.selectSignal(selectOrdersTotal);
  readonly currentPage = this.store.selectSignal(selectOrdersPage);
  readonly pageSize = this.store.selectSignal(selectOrdersPageSize);
  readonly loading = this.store.selectSignal(selectOrdersLoading);
  readonly error = this.store.selectSignal(selectOrdersError);
  readonly mode = this.store.selectSignal(selectOrdersMode);
  readonly saving = this.store.selectSignal(selectOrdersSaving);
  readonly vendorFilter = this.store.selectSignal(selectVendorFilter);
  readonly statusFilter = this.store.selectSignal(selectStatusFilter);
  readonly vendors = this.store.selectSignal(selectVendorOptions);
  readonly styles = this.store.selectSignal(selectStyleOptions);
  readonly current = this.store.selectSignal(selectCurrentPo);
  readonly selectedStyle = this.store.selectSignal(selectSelectedStyle);
  private readonly selectedVendor = this.store.selectSignal(selectSelectedVendor);
  readonly revisions = this.store.selectSignal(selectRevisions);
  readonly files = this.store.selectSignal(selectFiles);
  readonly panel = this.store.selectSignal(selectPanel);
  readonly panelSaving = this.store.selectSignal(selectPanelSaving);
  readonly panelError = this.store.selectSignal(selectPanelError);
  readonly cancelReasonId = this.store.selectSignal(selectCancelReasonId);
  readonly fileUploading = this.store.selectSignal(selectFileUploading);
  readonly fileError = this.store.selectSignal(selectFileError);
  readonly pendingRevision = this.store.selectSignal(selectPendingRevision);
  readonly currentRevisionNumber = this.store.selectSignal(selectCurrentRevisionNumber);
  readonly hasTechPack = this.store.selectSignal(selectHasTechPack);
  readonly styleCells = this.store.selectSignal(selectStyleCells);

  readonly cancelReasons = this.store.selectSignal(selectLookup('po-cancel-reasons'));
  readonly paymentTerms = this.store.selectSignal(selectLookup('payment-terms'));
  readonly amendmentReasons = this.store.selectSignal(selectLookup('amendment-reasons'));
  readonly channels = this.store.selectSignal(selectLookup('vendor-comm-channels'));
  readonly fabricOptions = this.store.selectSignal(selectLookup('fabric-responsibilities'));

  private readonly lineQtyById = signal<ReadonlyMap<string, number>>(new Map());
  readonly lineRows = computed<LineRow[]>(() => {
    const style = this.selectedStyle();
    if (!style) {
      return [];
    }
    const qtyById = this.lineQtyById();
    return style.sizeIds.flatMap((sizeId) =>
      style.colourIds.map((colourId) => ({ sizeId, colourId, qty: qtyById.get(lineKey(sizeId, colourId)) ?? 0 })),
    );
  });

  readonly form = buildPoForm(this.formBuilder);

  readonly fileUrlFor = (fileId: number): string => this.poApi.fileDownloadUrl(this.current()?.id ?? 0, fileId);

  constructor() {
    this.store.dispatch(PurchaseOrdersPageActions.opened());
    for (const typeKey of LOOKUP_TYPES) {
      this.store.dispatch(ReferenceLookupActions.requested({ typeKey, includeInactive: false }));
    }

    // Picking a vendor fills in that vendor's payment term and its default advance.
    effect(() => {
      const vendor = this.selectedVendor();
      if (vendor) {
        untracked(() => this.applyVendorDefaults(vendor));
      }
    });
  }

  onVendorFilterChange(vendorId: number | null): void {
    this.store.dispatch(PurchaseOrdersPageActions.vendorFilterChanged({ vendorId }));
  }

  onStatusFilterChange(statusId: number | null): void {
    this.store.dispatch(PurchaseOrdersPageActions.statusFilterChanged({ statusId }));
  }

  onVendorChange(vendorId: number | null): void {
    this.form.controls.vendorId.setValue(vendorId);
    if (vendorId === null) {
      return;
    }
    this.store.dispatch(PurchaseOrdersPageActions.vendorSelected({ vendorId }));
  }

  onStyleChange(styleId: number | null): void {
    this.form.controls.styleId.setValue(styleId);
    this.lineQtyById.set(new Map());
    this.store.dispatch(PurchaseOrdersPageActions.styleSelected({ styleId }));
  }

  setLineQty({ sizeId, colourId, qty }: LineQtyChange): void {
    const next = new Map(this.lineQtyById());
    next.set(lineKey(sizeId, colourId), qty);
    this.lineQtyById.set(next);
  }

  startCreate(): void {
    this.form.reset({
      vendorId: null,
      styleId: null,
      unitCost: 0,
      expectedDeliveryDate: '',
      paymentTermId: null,
      advancePercent: null,
      latestAcceptableDate: '',
      overTolerancePercent: null,
      underTolerancePercent: null,
      fabricResponsibilityId: null,
    });
    this.lineQtyById.set(new Map());
    this.store.dispatch(PurchaseOrdersPageActions.createStarted());
  }

  openDetail(summary: PoSummaryDto): void {
    this.store.dispatch(PurchaseOrdersPageActions.detailRequested({ id: summary.id }));
  }

  startEditDraft(): void {
    const po = this.current();
    if (!po) {
      return;
    }
    this.form.reset({
      vendorId: po.vendorId,
      styleId: po.styleId,
      unitCost: po.unitCost,
      expectedDeliveryDate: po.expectedDeliveryDate,
      paymentTermId: po.paymentTermId,
      advancePercent: po.advancePercent,
      latestAcceptableDate: po.latestAcceptableDate ?? '',
      overTolerancePercent: po.overTolerancePercent,
      underTolerancePercent: po.underTolerancePercent,
      fabricResponsibilityId: po.fabricResponsibilityId,
    });
    this.lineQtyById.set(new Map(po.lines.map((line) => [lineKey(line.sizeId, line.colourId), line.qty])));
    this.store.dispatch(PurchaseOrdersPageActions.draftEditStarted());
  }

  backToList(): void {
    this.store.dispatch(PurchaseOrdersPageActions.backToListClicked());
  }

  cancelForm(): void {
    this.store.dispatch(PurchaseOrdersPageActions.formCancelled());
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.store.dispatch(
        PurchaseOrdersPageActions.formInvalid({
          error: {
            status: 0,
            message: missingSummary(this.form.controls, {
              vendorId: 'Vendor',
              styleId: 'Style',
              unitCost: 'Unit cost',
              expectedDeliveryDate: 'Expected delivery date',
            }),
            fieldErrors: {},
          },
        }),
      );
      return;
    }

    const lines = this.lineRows().filter((row) => row.qty > 0).map((row) => ({ sizeId: row.sizeId, colourId: row.colourId, qty: row.qty }));
    if (lines.length === 0) {
      this.store.dispatch(
        PurchaseOrdersPageActions.formInvalid({
          error: { status: 0, message: 'At least one size/colour line is required.', fieldErrors: {} },
        }),
      );
      return;
    }

    const raw = this.form.getRawValue();
    this.store.dispatch(
      PurchaseOrdersPageActions.saveSubmitted({
        poId: this.mode() === 'create' ? null : this.current()!.id,
        draft: {
          vendorId: raw.vendorId!,
          styleId: raw.styleId!,
          unitCost: raw.unitCost,
          expectedDeliveryDate: raw.expectedDeliveryDate,
          lines,
          paymentTermId: raw.paymentTermId,
          advancePercent: raw.advancePercent,
          latestAcceptableDate: raw.latestAcceptableDate || null,
          overTolerancePercent: raw.overTolerancePercent,
          underTolerancePercent: raw.underTolerancePercent,
          fabricResponsibilityId: raw.fabricResponsibilityId,
        },
      }),
    );
  }

  openPanel(panel: Exclude<PoPanel, 'none'>): void {
    this.store.dispatch(PurchaseOrdersPageActions.panelOpened({ panel }));
  }

  closePanel(): void {
    this.store.dispatch(PurchaseOrdersPageActions.panelClosed());
  }

  /** AC-39: `sendWithoutTechPack` is the explicit "send anyway" from the confirmation step. */
  send(sendWithoutTechPack: boolean): void {
    const po = this.current();
    if (po) {
      this.store.dispatch(PurchaseOrdersPageActions.sendConfirmed({ poId: po.id, sendWithoutTechPack }));
    }
  }

  amend(value: AmendmentValue): void {
    const po = this.current();
    if (po) {
      this.store.dispatch(PurchaseOrdersPageActions.amendmentSubmitted({ poId: po.id, value }));
    }
  }

  recordVendorResponse(value: VendorResponseValue): void {
    const po = this.current();
    if (po) {
      this.store.dispatch(PurchaseOrdersPageActions.vendorResponseSubmitted({ poId: po.id, value }));
    }
  }

  acceptPending(revision: PoRevisionDto): void {
    this.decide(revision, 'accept', null);
  }

  rejectPending({ revision, note }: RevisionNoteDecision): void {
    this.decide(revision, 'reject', note);
  }

  withdrawPending({ revision, note }: RevisionNoteDecision): void {
    this.decide(revision, 'withdraw', note);
  }

  chooseCancelReason(reasonId: number | null): void {
    this.store.dispatch(PurchaseOrdersPageActions.cancelReasonChosen({ reasonId }));
  }

  cancel(): void {
    const po = this.current();
    const reasonId = this.cancelReasonId();
    if (!po || reasonId === null) {
      return;
    }
    this.store.dispatch(PurchaseOrdersPageActions.cancelConfirmed({ poId: po.id, reasonId }));
  }

  uploadFile(upload: PoFileUpload): void {
    const po = this.current();
    if (po) {
      this.store.dispatch(PurchaseOrdersPageActions.fileUploadSubmitted({ poId: po.id, upload }));
    }
  }

  removeFile(file: PoFileDto): void {
    const po = this.current();
    if (po) {
      this.store.dispatch(PurchaseOrdersPageActions.fileRemovalSubmitted({ poId: po.id, fileId: file.id }));
    }
  }

  private decide(revision: PoRevisionDto, kind: 'accept' | 'reject' | 'withdraw', note: string | null): void {
    this.store.dispatch(
      PurchaseOrdersPageActions.revisionDecisionSubmitted({
        decision: { kind, poId: revision.poId, revisionNumber: revision.revisionNumber, note },
      }),
    );
  }

  private applyVendorDefaults(vendor: VendorDto): void {
    this.form.controls.paymentTermId.setValue(vendor.paymentTermId);
    const term = this.paymentTerms().find((t) => t.id === vendor.paymentTermId);
    this.form.controls.advancePercent.setValue(term?.defaultAdvancePercent ?? null);
  }

  changePage(page: number): void {
    this.store.dispatch(PurchaseOrdersPageActions.pageChanged({ page }));
  }

  changePageSize(size: number): void {
    this.store.dispatch(PurchaseOrdersPageActions.pageSizeChanged({ pageSize: size }));
  }
}
