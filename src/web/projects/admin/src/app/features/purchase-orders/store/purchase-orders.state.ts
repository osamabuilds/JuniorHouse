import { ApiError } from '@core/http';
import { DEFAULT_PAGE_SIZE } from '@shared';
import { StyleDto, StyleSummaryDto } from '@features/styles';
import { VendorSummaryDto } from '@features/vendors';
import { VendorDto } from '@features/vendors';
import { PoDto, PoFileDto, PoPageMode, PoPanel, PoRevisionDto, PoSummaryDto, VendorPoViewDto } from '../models';

export const PURCHASE_ORDERS_FEATURE_KEY = 'purchaseOrders';

export interface PurchaseOrdersState {
  readonly orders: readonly PoSummaryDto[];
  readonly total: number;
  readonly page: number;
  readonly pageSize: number;
  readonly loading: boolean;
  readonly error: ApiError | null;
  readonly mode: PoPageMode;
  readonly saving: boolean;

  readonly vendorFilter: number | null;
  readonly statusFilter: number | null;

  /** Options for the vendor / style pickers. */
  readonly vendors: readonly VendorSummaryDto[];
  readonly styles: readonly StyleSummaryDto[];

  readonly current: PoDto | null;
  readonly selectedStyle: StyleDto | null;
  /** The vendor picked in the form, whose defaults (payment term) the form copies. */
  readonly selectedVendor: VendorDto | null;
  readonly revisions: readonly PoRevisionDto[];
  readonly files: readonly PoFileDto[];

  readonly panel: PoPanel;
  readonly panelSaving: boolean;
  readonly panelError: ApiError | null;
  readonly cancelReasonId: number | null;

  readonly fileUploading: boolean;
  readonly fileError: ApiError | null;

  /** The vendor-facing read-only view (SCRUM-93 task 51). */
  readonly vendorView: VendorPoViewDto | null;
  readonly vendorViewError: ApiError | null;
}

export const initialPurchaseOrdersState: PurchaseOrdersState = {
  orders: [],
  total: 0,
  page: 1,
  pageSize: DEFAULT_PAGE_SIZE,
  loading: false,
  error: null,
  mode: 'list',
  saving: false,
  vendorFilter: null,
  statusFilter: null,
  vendors: [],
  styles: [],
  current: null,
  selectedStyle: null,
  selectedVendor: null,
  revisions: [],
  files: [],
  panel: 'none',
  panelSaving: false,
  panelError: null,
  cancelReasonId: null,
  fileUploading: false,
  fileError: null,
  vendorView: null,
  vendorViewError: null,
};
