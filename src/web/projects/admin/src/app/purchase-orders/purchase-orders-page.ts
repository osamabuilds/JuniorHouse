import { KeyValuePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { Component, computed, inject, signal } from '@angular/core';
import { Observable, of, switchMap } from 'rxjs';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { CatalogApiService, StyleDto, StyleSummaryDto } from '../styles/catalog-api.service';
import { ApiError } from '../core/api-error';
import { LookupDto, ReferenceApiService } from '../reference-data/reference-api.service';
import { AppDatePipe } from '../shared/app-date.pipe';
import { ControlErrors, missingSummary } from '../shared/control-errors';
import { FieldErrors } from '../shared/field-errors';
import { LookupNames } from '../shared/lookup-names';
import { inputNumber, selectNumberOrNull } from '../shared/dom-events';
import { VendorApiService, VendorSummaryDto } from '../vendors/vendor-api.service';
import { AmendForm, StyleCell } from './amend-form';
import { PendingRevisionActions } from './pending-revision-actions';
import { PoFilesPanel } from './po-files-panel';
import {
  AmendmentValue,
  PO_STATUS_LABELS,
  PoApiService,
  PoDto,
  PoFileDto,
  PoRevisionDto,
  PoSummaryDto,
  TECH_PACK_SPEC,
  UpdatePoValue,
  VendorResponseValue,
} from './po-api.service';
import { RevisionHistory } from './revision-history';
import { SendConfirm } from './send-confirm';
import { VendorResponseForm } from './vendor-response-form';

const DRAFT = 1;
const SENT_TO_VENDOR = 2;
const ACKNOWLEDGED = 3;

type Mode = 'list' | 'create' | 'edit' | 'detail';

interface LineRow {
  readonly sizeId: number;
  readonly colourId: number;
  qty: number;
}

/**
 * SCRUM-174: PO list (filter by vendor/status/date), create/edit (Draft only, AC-9/AC-13) and a
 * detail view with the status timeline (AC-14) and Send/Acknowledge/Cancel actions, each disabled
 * when illegal for the PO's current status.
 */
@Component({
  selector: 'app-purchase-orders-page',
  imports: [
    ReactiveFormsModule,
    RouterLink,
    FieldErrors,
    AppDatePipe,
    ControlErrors,
    KeyValuePipe,
    AmendForm,
    PendingRevisionActions,
    PoFilesPanel,
    RevisionHistory,
    SendConfirm,
    VendorResponseForm,
  ],
  templateUrl: './purchase-orders-page.html',
  styleUrl: './purchase-orders-page.scss',
})
export class PurchaseOrdersPage {
  private readonly poApi = inject(PoApiService);
  private readonly vendorApi = inject(VendorApiService);
  private readonly catalogApi = inject(CatalogApiService);
  private readonly referenceApi = inject(ReferenceApiService);
  private readonly formBuilder = inject(FormBuilder);
  readonly names = inject(LookupNames);

  readonly statusLabels = PO_STATUS_LABELS;
  readonly orders = signal<PoSummaryDto[]>([]);
  readonly loading = signal(false);
  readonly error = signal<ApiError | null>(null);
  readonly mode = signal<Mode>('list');
  readonly saving = signal(false);

  readonly vendorFilter = signal<number | null>(null);
  readonly statusFilter = signal<number | null>(null);

  readonly vendors = signal<VendorSummaryDto[]>([]);
  readonly styles = signal<StyleSummaryDto[]>([]);
  readonly cancelReasons = signal<LookupDto[]>([]);
  readonly paymentTerms = signal<LookupDto[]>([]);
  readonly amendmentReasons = signal<LookupDto[]>([]);
  readonly channels = signal<LookupDto[]>([]);
  readonly fabricOptions = signal<LookupDto[]>([]);
  readonly revisions = signal<PoRevisionDto[]>([]);
  readonly files = signal<PoFileDto[]>([]);

  /** Which inline panel is open in the detail view (amend / vendor response / send confirmation). */
  readonly panel = signal<'none' | 'amend' | 'response' | 'send'>('none');
  readonly panelSaving = signal(false);
  readonly panelError = signal<ApiError | null>(null);

  readonly pendingRevision = computed(() => this.revisions().find((revision) => revision.statusId === 1) ?? null);
  readonly currentRevisionNumber = computed(() => this.revisions().find((revision) => revision.statusId === 2)?.revisionNumber ?? 0);
  readonly hasTechPack = computed(() => this.files().some((file) => file.categoryId === TECH_PACK_SPEC));
  readonly styleCells = computed<StyleCell[]>(() => {
    const style = this.selectedStyle();
    return style ? style.sizeIds.flatMap((sizeId) => style.colourIds.map((colourId) => ({ sizeId, colourId }))) : [];
  });
  readonly selectedStyle = signal<StyleDto | null>(null);
  readonly current = signal<PoDto | null>(null);

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

  readonly cancelReasonId = signal<number | null>(null);

  readonly form = this.formBuilder.nonNullable.group({
    vendorId: this.formBuilder.control<number | null>(null, Validators.required),
    styleId: this.formBuilder.control<number | null>(null, Validators.required),
    unitCost: [0, [Validators.required, Validators.min(0.01)]],
    expectedDeliveryDate: ['', Validators.required],
    paymentTermId: this.formBuilder.control<number | null>(null),
    advancePercent: this.formBuilder.control<number | null>(null),
    latestAcceptableDate: [''],
    overTolerancePercent: this.formBuilder.control<number | null>(null),
    underTolerancePercent: this.formBuilder.control<number | null>(null),
    fabricResponsibilityId: this.formBuilder.control<number | null>(null),
  });

  constructor() {
    this.referenceApi.list('payment-terms', false).subscribe((items) => this.paymentTerms.set(items));
    this.referenceApi.list('amendment-reasons', false).subscribe((items) => this.amendmentReasons.set(items));
    this.referenceApi.list('vendor-comm-channels', false).subscribe((items) => this.channels.set(items));
    this.referenceApi.list('fabric-responsibilities', false).subscribe((items) => this.fabricOptions.set(items));
    this.loadOrders();
    this.vendorApi.search('', null, true).subscribe((items) => this.vendors.set(items));
    this.catalogApi.search('', null, true).subscribe((items) => this.styles.set(items));
    this.referenceApi.list('po-cancel-reasons', false).subscribe((items) => this.cancelReasons.set(items));
  }

  applyFilters(): void {
    this.loadOrders();
  }

  onVendorFilterChange(event: Event): void {
    this.vendorFilter.set(selectNumberOrNull(event));
    this.applyFilters();
  }

  onStatusFilterChange(event: Event): void {
    this.statusFilter.set(selectNumberOrNull(event));
    this.applyFilters();
  }

  onCancelReasonChange(event: Event): void {
    this.cancelReasonId.set(selectNumberOrNull(event));
  }

  onVendorSelectChange(event: Event): void {
    this.onVendorChange(selectNumberOrNull(event));
  }

  onStyleSelectChange(event: Event): void {
    this.onStyleChange(selectNumberOrNull(event));
  }

  onLineQtyInput(sizeId: number, colourId: number, event: Event): void {
    this.setLineQty(sizeId, colourId, inputNumber(event));
  }

  onVendorChange(vendorId: number | null): void {
    this.form.controls.vendorId.setValue(vendorId);
    if (vendorId === null) {
      return;
    }
    this.vendorApi.getById(vendorId).subscribe((vendor) => {
      this.form.controls.paymentTermId.setValue(vendor.paymentTermId);
      this.referenceApi.list('payment-terms', false).subscribe((terms) => {
        const term = terms.find((t) => t.id === vendor.paymentTermId);
        this.form.controls.advancePercent.setValue(term?.defaultAdvancePercent ?? null);
      });
    });
  }

  onStyleChange(styleId: number | null): void {
    this.form.controls.styleId.setValue(styleId);
    this.lineQtyById.set(new Map());
    if (styleId === null) {
      this.selectedStyle.set(null);
      return;
    }
    this.catalogApi.getById(styleId).subscribe((style) => this.selectedStyle.set(style));
  }

  setLineQty(sizeId: number, colourId: number, qty: number): void {
    const next = new Map(this.lineQtyById());
    next.set(lineKey(sizeId, colourId), qty);
    this.lineQtyById.set(next);
  }

  startCreate(): void {
    this.mode.set('create');
    this.error.set(null);
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
    this.selectedStyle.set(null);
    this.lineQtyById.set(new Map());
  }

  openDetail(summary: PoSummaryDto): void {
    this.loading.set(true);
    this.poApi.getById(summary.id).subscribe((po) => {
      this.showDetail(po);
      this.loading.set(false);
    });
  }

  /** Opens the detail view for a PO and loads what sits beside it: revisions, files and the style's size run. */
  private showDetail(po: PoDto): void {
    this.current.set(po);
    this.mode.set('detail');
    this.panel.set('none');
    this.panelError.set(null);
    this.loadDetailExtras(po);
    if (this.selectedStyle()?.id !== po.styleId) {
      this.catalogApi.getById(po.styleId).subscribe((style) => this.selectedStyle.set(style));
    }
  }

  private loadDetailExtras(po: PoDto): void {
    this.poApi.listFiles(po.id).subscribe((files) => this.files.set(files));
    if (po.statusId === DRAFT) {
      this.revisions.set([]);
      return;
    }
    this.poApi.getRevisions(po.id).subscribe((revisions) => this.revisions.set(revisions));
  }

  /** Re-reads the PO and its side data after any change made from the detail view. */
  refreshDetail(): void {
    const po = this.current();
    if (!po) {
      return;
    }
    this.poApi.getById(po.id).subscribe((updated) => {
      this.current.set(updated);
      this.loadDetailExtras(updated);
    });
  }

  startEditDraft(): void {
    const po = this.current();
    if (!po) {
      return;
    }
    this.mode.set('edit');
    this.error.set(null);
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
    this.catalogApi.getById(po.styleId).subscribe((style) => {
      this.selectedStyle.set(style);
      this.lineQtyById.set(new Map(po.lines.map((line) => [lineKey(line.sizeId, line.colourId), line.qty])));
    });
  }

  backToList(): void {
    this.mode.set('list');
    this.current.set(null);
    this.loadOrders();
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.error.set({
        status: 0,
        message: missingSummary(this.form.controls, {
          vendorId: 'Vendor',
          styleId: 'Style',
          unitCost: 'Unit cost',
          expectedDeliveryDate: 'Expected delivery date',
        }),
        fieldErrors: {},
      });
      return;
    }

    const lines = this.lineRows().filter((row) => row.qty > 0).map((row) => ({ sizeId: row.sizeId, colourId: row.colourId, qty: row.qty }));
    if (lines.length === 0) {
      this.error.set({ status: 0, message: 'At least one size/colour line is required.', fieldErrors: {} });
      return;
    }

    this.saving.set(true);
    this.error.set(null);
    const raw = this.form.getRawValue();

    // The commercial terms (latest acceptable date, tolerances, fabric responsibility) are set on
    // update; the create call takes only the core fields, so a new PO with terms is created and
    // then updated in one go.
    const updateValue = (po: { paymentTermId: number; advancePercent: number }): UpdatePoValue => ({
      unitCost: raw.unitCost,
      expectedDeliveryDate: raw.expectedDeliveryDate,
      paymentTermId: raw.paymentTermId ?? po.paymentTermId,
      advancePercent: raw.advancePercent ?? po.advancePercent,
      lines,
      latestAcceptableDate: raw.latestAcceptableDate || null,
      overTolerancePercent: raw.overTolerancePercent,
      underTolerancePercent: raw.underTolerancePercent,
      fabricResponsibilityId: raw.fabricResponsibilityId,
    });
    const hasTerms =
      !!raw.latestAcceptableDate ||
      raw.overTolerancePercent !== null ||
      raw.underTolerancePercent !== null ||
      raw.fabricResponsibilityId !== null;

    const request =
      this.mode() === 'create'
        ? this.poApi
            .create({
              vendorId: raw.vendorId!,
              styleId: raw.styleId!,
              unitCost: raw.unitCost,
              expectedDeliveryDate: raw.expectedDeliveryDate,
              lines,
              paymentTermId: raw.paymentTermId,
              advancePercent: raw.advancePercent,
            })
            .pipe(switchMap((created) => (hasTerms ? this.poApi.update(created.id, updateValue(created)) : of(created))))
        : this.poApi.update(this.current()!.id, updateValue(this.current()!));

    request.subscribe({
      next: (po) => {
        this.saving.set(false);
        this.showDetail(po);
      },
      error: (error: ApiError) => {
        this.saving.set(false);
        this.error.set(error);
      },
    });
  }

  openPanel(panel: 'amend' | 'response' | 'send'): void {
    this.panel.set(panel);
    this.panelError.set(null);
    this.error.set(null);
  }

  closePanel(): void {
    this.panel.set('none');
    this.panelError.set(null);
  }

  /** AC-39: `sendWithoutTechPack` is the explicit "send anyway" from the confirmation step. */
  send(sendWithoutTechPack: boolean): void {
    const po = this.current();
    if (!po) {
      return;
    }
    this.error.set(null);
    this.poApi.send(po.id, sendWithoutTechPack).subscribe({
      next: (updated) => {
        this.current.set(updated);
        this.closePanel();
        this.loadDetailExtras(updated);
      },
      error: (error: ApiError) => {
        this.error.set(error);
        this.closePanel();
      },
    });
  }

  amend(value: AmendmentValue): void {
    const po = this.current();
    if (!po) {
      return;
    }
    this.runPanelAction(this.poApi.createAmendment(po.id, value));
  }

  recordVendorResponse(value: VendorResponseValue): void {
    const po = this.current();
    if (!po) {
      return;
    }
    this.panelSaving.set(true);
    this.panelError.set(null);
    this.poApi.recordVendorResponse(po.id, value).subscribe({
      next: (result) => {
        this.panelSaving.set(false);
        this.closePanel();
        this.refreshDetail();
        if (result.suggestedCancelReasonId !== null) {
          // AC-29: a declined PO isn't cancelled automatically - Cancel opens with the reason pre-filled.
          this.cancelReasonId.set(result.suggestedCancelReasonId);
        }
      },
      error: (error: ApiError) => {
        this.panelSaving.set(false);
        this.panelError.set(error);
      },
    });
  }

  acceptPending(revision: PoRevisionDto): void {
    this.runRevisionAction(this.poApi.acceptRevision(revision.poId, revision.revisionNumber));
  }

  rejectPending(revision: PoRevisionDto, note: string | null): void {
    this.runRevisionAction(this.poApi.rejectRevision(revision.poId, revision.revisionNumber, note));
  }

  withdrawPending(revision: PoRevisionDto, note: string | null): void {
    this.runRevisionAction(this.poApi.withdrawRevision(revision.poId, revision.revisionNumber, note));
  }

  private runPanelAction(action: Observable<unknown>): void {
    this.panelSaving.set(true);
    this.panelError.set(null);
    action.subscribe({
      next: () => {
        this.panelSaving.set(false);
        this.closePanel();
        this.refreshDetail();
      },
      error: (error: ApiError) => {
        this.panelSaving.set(false);
        this.panelError.set(error);
      },
    });
  }

  private runRevisionAction(action: Observable<unknown>): void {
    this.error.set(null);
    action.subscribe({
      next: () => this.refreshDetail(),
      error: (error: ApiError) => this.error.set(error),
    });
  }

  cancel(): void {
    const po = this.current();
    const reasonId = this.cancelReasonId();
    if (!po || reasonId === null) {
      return;
    }
    this.poApi.cancel(po.id, reasonId).subscribe({
      next: (updated) => {
        this.current.set(updated);
        this.loadDetailExtras(updated); // an open Pending revision is auto-withdrawn by the cancel (AC-21)
        this.cancelReasonId.set(null);
      },
      error: (error: ApiError) => this.error.set(error),
    });
  }

  canEdit(po: PoDto): boolean {
    return po.statusId === DRAFT;
  }

  canSend(po: PoDto): boolean {
    return po.statusId === DRAFT;
  }

  /** AC-25..AC-31: a vendor response is recorded against a PO that has been sent and not yet acknowledged. */
  canRecordResponse(po: PoDto): boolean {
    return po.statusId === SENT_TO_VENDOR;
  }

  /** AC-9/AC-10: only a sent or acknowledged PO is amended; a Draft is simply edited. */
  canAmend(po: PoDto): boolean {
    return po.statusId === SENT_TO_VENDOR || po.statusId === ACKNOWLEDGED;
  }

  canCancel(po: PoDto): boolean {
    return po.statusId === DRAFT || po.statusId === SENT_TO_VENDOR || po.statusId === ACKNOWLEDGED;
  }

  fieldErrors(field: string): readonly string[] {
    return this.error()?.fieldErrors[field] ?? [];
  }

  /** One plain sentence telling staff where this PO stands and what they can do next. */
  statusHelp(po: PoDto): string {
    switch (po.statusId) {
      case DRAFT:
        return 'This order is still a draft, so only you can see it. Check the details, attach the tech pack, then send it to the vendor.';
      case SENT_TO_VENDOR:
        return 'This order has been sent and is waiting for the vendor. Record what the vendor says, or amend the order if something changes.';
      case ACKNOWLEDGED:
        return 'The vendor has confirmed this order. From now on, any change to the price, dates or quantities has to be agreed as an amendment.';
      default:
        return 'This order is cancelled and closed. Its details and files are kept for the record.';
    }
  }

  /** Download link for an evidence file behind a vendor communication on the open PO. */
  readonly evidenceUrl = (fileId: number): string => this.poApi.fileDownloadUrl(this.current()?.id ?? 0, fileId);

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

  private loadOrders(): void {
    this.loading.set(true);
    this.error.set(null);

    this.poApi.search(this.vendorFilter(), this.statusFilter(), null, null).subscribe({
      next: (orders) => {
        this.orders.set(orders);
        this.loading.set(false);
      },
      error: (error: ApiError) => {
        this.error.set(error);
        this.loading.set(false);
      },
    });
  }
}

function lineKey(sizeId: number, colourId: number): string {
  return `${sizeId}-${colourId}`;
}
