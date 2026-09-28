import { KeyValuePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { CatalogApiService, StyleDto, StyleSummaryDto } from '../styles/catalog-api.service';
import { ApiError } from '../core/api-error';
import { LookupDto, ReferenceApiService } from '../reference-data/reference-api.service';
import { FieldErrors } from '../shared/field-errors';
import { inputNumber, selectNumberOrNull } from '../shared/dom-events';
import { VendorApiService, VendorSummaryDto } from '../vendors/vendor-api.service';
import { PO_STATUS_LABELS, PoApiService, PoDto, PoSummaryDto } from './po-api.service';

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
  imports: [ReactiveFormsModule, FieldErrors, KeyValuePipe],
  templateUrl: './purchase-orders-page.html',
  styleUrl: './purchase-orders-page.scss',
})
export class PurchaseOrdersPage {
  private readonly poApi = inject(PoApiService);
  private readonly vendorApi = inject(VendorApiService);
  private readonly catalogApi = inject(CatalogApiService);
  private readonly referenceApi = inject(ReferenceApiService);
  private readonly formBuilder = inject(FormBuilder);

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
  });

  constructor() {
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
    this.form.reset({ vendorId: null, styleId: null, unitCost: 0, expectedDeliveryDate: '', paymentTermId: null, advancePercent: null });
    this.selectedStyle.set(null);
    this.lineQtyById.set(new Map());
  }

  openDetail(summary: PoSummaryDto): void {
    this.loading.set(true);
    this.poApi.getById(summary.id).subscribe((po) => {
      this.current.set(po);
      this.mode.set('detail');
      this.loading.set(false);
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

    const request =
      this.mode() === 'create'
        ? this.poApi.create({
            vendorId: raw.vendorId!,
            styleId: raw.styleId!,
            unitCost: raw.unitCost,
            expectedDeliveryDate: raw.expectedDeliveryDate,
            lines,
            paymentTermId: raw.paymentTermId,
            advancePercent: raw.advancePercent,
          })
        : this.poApi.update(this.current()!.id, {
            unitCost: raw.unitCost,
            expectedDeliveryDate: raw.expectedDeliveryDate,
            paymentTermId: raw.paymentTermId!,
            advancePercent: raw.advancePercent!,
            lines,
          });

    request.subscribe({
      next: (po) => {
        this.saving.set(false);
        this.current.set(po);
        this.mode.set('detail');
      },
      error: (error: ApiError) => {
        this.saving.set(false);
        this.error.set(error);
      },
    });
  }

  send(): void {
    const po = this.current();
    if (!po) {
      return;
    }
    this.poApi.send(po.id).subscribe({
      next: (updated) => this.current.set(updated),
      error: (error: ApiError) => this.error.set(error),
    });
  }

  acknowledge(): void {
    const po = this.current();
    if (!po) {
      return;
    }
    this.poApi.acknowledge(po.id).subscribe({
      next: (updated) => this.current.set(updated),
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

  canAcknowledge(po: PoDto): boolean {
    return po.statusId === SENT_TO_VENDOR;
  }

  canCancel(po: PoDto): boolean {
    return po.statusId === DRAFT || po.statusId === SENT_TO_VENDOR || po.statusId === ACKNOWLEDGED;
  }

  fieldErrors(field: string): readonly string[] {
    return this.error()?.fieldErrors[field] ?? [];
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
