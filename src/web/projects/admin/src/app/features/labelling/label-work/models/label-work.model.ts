/** A work item as the API returns it (docs/specs/labelling/label-work/spec.md). */
export interface LabelWorkItemDto {
  readonly id: number;
  readonly subBatchId: number;
  readonly qcBatchId: number;
  readonly poId: number;
  readonly styleId: number | null;
  readonly styleCode: string | null;
  readonly styleName: string | null;
  readonly styleIsActive: boolean;
  readonly sizeId: number;
  readonly sizeName: string;
  readonly colourId: number;
  readonly colourName: string;
  readonly qty: number;
  readonly outcomeCode: 'Accepted' | 'Concession';
  readonly statusCode: 'Ready' | 'NeedsAttention';
  readonly parkReasonCode: 'NoStyle' | 'StyleRetired' | 'UnknownCode' | null;
  readonly variantId: number | null;
  readonly skuCode: string | null;
  readonly gtin: string | null;
  readonly printedQty: number;
  readonly activeQty: number;
  readonly remainingQty: number;
  readonly priceChanged: boolean;
  readonly createdAt: string;
}

/** A configured printer staff may choose. The API never accepts a printer address, only this code (PJ-12). */
export interface PrinterDto {
  readonly id: number;
  readonly code: string;
  readonly name: string;
  readonly formatCode: 'Zpl' | 'Pdf';
}

/** The job the API created when tags were queued (only what the worklist needs to confirm it). */
export interface QueuedJobDto {
  readonly id: number;
  readonly jobNo: string;
  readonly qty: number;
  readonly statusCode: string;
  readonly printerCode: string;
}

export type WorkStatusFilter = 'all' | 'ready' | 'attention';

export interface LabelWorkFilters {
  readonly status: WorkStatusFilter;
  readonly priceFlagOnly: boolean;
  readonly page: number;
  readonly pageSize: number;
}

export interface QueueTagsValue {
  readonly qty: number;
  readonly printerCode: string | null;
  readonly actingPerson: string;
}

export interface ResolveWorkItemValue {
  readonly confirmRetiredStyle: boolean;
  readonly actingPerson: string;
}
