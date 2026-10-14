/** A print job as the API returns it (docs/specs/labelling/print-jobs/spec.md). */
export interface PrintJobDto {
  readonly id: number;
  readonly jobNo: string;
  readonly kindCode: 'Original' | 'Reprint';
  readonly statusCode: 'Queued' | 'Printing' | 'Printed' | 'Failed' | 'Cancelled';
  readonly variantGtinId: number;
  readonly workItemId: number | null;
  readonly skuCode: string;
  readonly gtin: string;
  readonly qty: number;
  readonly priceSnapshot: number;
  readonly printerCode: string;
  readonly attemptCount: number;
  readonly lastError: string | null;
  readonly reprintReasonCode: string | null;
  readonly note: string | null;
  readonly requestedBy: string;
  readonly manualActionBy: string | null;
  readonly createdAt: string;
  readonly nextAttemptAt: string;
}

/** A configured printer staff may choose. The API never accepts a printer address, only this code (PJ-12). */
export interface PrinterDto {
  readonly id: number;
  readonly code: string;
  readonly name: string;
  readonly formatCode: 'Zpl' | 'Pdf';
}

export type JobStatusFilter = 'all' | 'queued' | 'printing' | 'printed' | 'failed' | 'cancelled';

export interface PrintJobFilters {
  readonly status: JobStatusFilter;
  /** When set, the screen shows the print history of one variant instead of the whole queue (PJ-6). */
  readonly variantId: number | null;
  readonly page: number;
  readonly pageSize: number;
}

export interface ReprintValue {
  readonly qty: number;
  readonly reasonId: number;
  readonly note: string | null;
  readonly printerCode: string | null;
  readonly actingPerson: string;
}
