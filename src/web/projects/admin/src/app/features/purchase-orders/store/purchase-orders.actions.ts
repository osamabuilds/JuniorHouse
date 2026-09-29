import { createActionGroup, emptyProps, props } from '@ngrx/store';
import { ApiError } from '@core/http';
import { StyleDto, StyleSummaryDto } from '@features/styles';
import { VendorDto, VendorSummaryDto } from '@features/vendors';
import {
  AmendmentValue,
  PoDto,
  PoFileDto,
  PoFileUpload,
  PoFormDraft,
  PoPanel,
  PoRevisionDto,
  PoSummaryDto,
  RevisionDecision,
  VendorPoViewDto,
  VendorResponseValue,
} from '../models';

export const PurchaseOrdersPageActions = createActionGroup({
  source: 'Purchase Orders Page',
  events: {
    Opened: emptyProps(),
    'Vendor Filter Changed': props<{ vendorId: number | null }>(),
    'Status Filter Changed': props<{ statusId: number | null }>(),
    'Create Started': emptyProps(),
    'Draft Edit Started': emptyProps(),
    'Form Cancelled': emptyProps(),
    'Form Invalid': props<{ error: ApiError }>(),
    'Vendor Selected': props<{ vendorId: number }>(),
    'Style Selected': props<{ styleId: number | null }>(),
    'Save Submitted': props<{ poId: number | null; draft: PoFormDraft }>(),
    'Detail Requested': props<{ id: number }>(),
    'Back To List Clicked': emptyProps(),
    'Panel Opened': props<{ panel: Exclude<PoPanel, 'none'> }>(),
    'Panel Closed': emptyProps(),
    'Send Confirmed': props<{ poId: number; sendWithoutTechPack: boolean }>(),
    'Amendment Submitted': props<{ poId: number; value: AmendmentValue }>(),
    'Vendor Response Submitted': props<{ poId: number; value: VendorResponseValue }>(),
    'Revision Decision Submitted': props<{ decision: RevisionDecision }>(),
    'Cancel Reason Chosen': props<{ reasonId: number | null }>(),
    'Cancel Confirmed': props<{ poId: number; reasonId: number }>(),
    'File Upload Submitted': props<{ poId: number; upload: PoFileUpload }>(),
    'File Removal Submitted': props<{ poId: number; fileId: number }>(),
  },
});

export const VendorPoViewActions = createActionGroup({
  source: 'Vendor PO View',
  events: {
    Opened: props<{ poId: number }>(),
  },
});

export const PurchaseOrdersApiActions = createActionGroup({
  source: 'Purchase Orders API',
  events: {
    'Load Orders Succeeded': props<{ orders: PoSummaryDto[] }>(),
    'Load Orders Failed': props<{ error: ApiError }>(),
    'Load Vendors Succeeded': props<{ vendors: VendorSummaryDto[] }>(),
    'Load Styles Succeeded': props<{ styles: StyleSummaryDto[] }>(),
    'Load Vendor Succeeded': props<{ vendor: VendorDto }>(),
    'Load Style Succeeded': props<{ style: StyleDto }>(),
    'Load Detail Succeeded': props<{ po: PoDto }>(),
    'Load Detail Failed': props<{ error: ApiError }>(),
    'Refresh Detail Succeeded': props<{ po: PoDto }>(),
    'Refresh Detail Failed': props<{ error: ApiError }>(),
    'Load Files Succeeded': props<{ files: PoFileDto[] }>(),
    'Load Revisions Succeeded': props<{ revisions: PoRevisionDto[] }>(),
    'Save Succeeded': props<{ po: PoDto }>(),
    'Save Failed': props<{ error: ApiError }>(),
    'Send Succeeded': props<{ po: PoDto }>(),
    'Send Failed': props<{ error: ApiError }>(),
    'Amendment Succeeded': emptyProps(),
    'Amendment Failed': props<{ error: ApiError }>(),
    'Vendor Response Succeeded': props<{ suggestedCancelReasonId: number | null }>(),
    'Vendor Response Failed': props<{ error: ApiError }>(),
    'Revision Decision Succeeded': emptyProps(),
    'Revision Decision Failed': props<{ error: ApiError }>(),
    'Cancel Succeeded': props<{ po: PoDto }>(),
    'Cancel Failed': props<{ error: ApiError }>(),
    'File Upload Succeeded': emptyProps(),
    'File Upload Failed': props<{ error: ApiError }>(),
    'File Removal Succeeded': emptyProps(),
    'File Removal Failed': props<{ error: ApiError }>(),
    'Load Vendor View Succeeded': props<{ view: VendorPoViewDto }>(),
    'Load Vendor View Failed': props<{ error: ApiError }>(),
  },
});
