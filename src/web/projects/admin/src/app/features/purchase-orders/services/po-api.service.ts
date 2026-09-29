import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiClient } from '@core/http';
import {
  AmendmentValue,
  CreatePoValue,
  PoDto,
  PoFileDto,
  PoRevisionDto,
  PoSummaryDto,
  UpdatePoValue,
  VendorPoViewDto,
  VendorResponseResult,
  VendorResponseValue,
} from '../models';

@Injectable({ providedIn: 'root' })
export class PoApiService {
  private readonly api = inject(ApiClient);

  search(vendorId: number | null, statusId: number | null, deliveryFrom: string | null, deliveryTo: string | null): Observable<PoSummaryDto[]> {
    return this.api.get<PoSummaryDto[]>('/api/purchase-orders', {
      vendorId: vendorId ?? undefined,
      statusId: statusId ?? undefined,
      deliveryFrom: deliveryFrom || undefined,
      deliveryTo: deliveryTo || undefined,
    });
  }

  getById(id: number): Observable<PoDto> {
    return this.api.get<PoDto>(`/api/purchase-orders/${id}`);
  }

  create(value: CreatePoValue): Observable<PoDto> {
    return this.api.post<PoDto>('/api/purchase-orders', value);
  }

  update(id: number, value: UpdatePoValue): Observable<PoDto> {
    return this.api.put<PoDto>(`/api/purchase-orders/${id}`, value);
  }

  /** `sendWithoutTechPack` is the explicit "send anyway" (AC-39); the API rejects a send with no tech pack without it. */
  send(id: number, sendWithoutTechPack = false): Observable<PoDto> {
    return this.api.post<PoDto>(`/api/purchase-orders/${id}/send?sendWithoutTechPack=${sendWithoutTechPack}`, {});
  }

  cancel(id: number, cancelReasonId: number): Observable<PoDto> {
    return this.api.post<PoDto>(`/api/purchase-orders/${id}/cancel`, { cancelReasonId });
  }

  getRevisions(id: number): Observable<PoRevisionDto[]> {
    return this.api.get<PoRevisionDto[]>(`/api/purchase-orders/${id}/revisions`);
  }

  createAmendment(id: number, value: AmendmentValue): Observable<PoRevisionDto> {
    return this.api.post<PoRevisionDto>(`/api/purchase-orders/${id}/amendments`, value);
  }

  acceptRevision(id: number, revisionNumber: number): Observable<PoDto> {
    return this.api.post<PoDto>(`/api/purchase-orders/${id}/amendments/${revisionNumber}/accept`, {});
  }

  rejectRevision(id: number, revisionNumber: number, note: string | null): Observable<PoDto> {
    return this.api.post<PoDto>(`/api/purchase-orders/${id}/amendments/${revisionNumber}/reject`, { note });
  }

  withdrawRevision(id: number, revisionNumber: number, note: string | null): Observable<PoDto> {
    return this.api.post<PoDto>(`/api/purchase-orders/${id}/amendments/${revisionNumber}/withdraw`, { note });
  }

  recordVendorResponse(id: number, value: VendorResponseValue): Observable<VendorResponseResult> {
    return this.api.post<VendorResponseResult>(`/api/purchase-orders/${id}/vendor-response`, value);
  }

  listFiles(id: number): Observable<PoFileDto[]> {
    return this.api.get<PoFileDto[]>(`/api/purchase-orders/${id}/files`);
  }

  uploadFile(id: number, categoryId: number, file: File): Observable<PoFileDto> {
    const form = new FormData();
    form.append('categoryId', String(categoryId));
    form.append('file', file, file.name);
    return this.api.postForm<PoFileDto>(`/api/purchase-orders/${id}/files`, form);
  }

  removeFile(id: number, fileId: number): Observable<void> {
    return this.api.delete<void>(`/api/purchase-orders/${id}/files/${fileId}`);
  }

  fileDownloadUrl(id: number, fileId: number): string {
    return this.api.url(`/api/purchase-orders/${id}/files/${fileId}`);
  }

  getVendorView(id: number): Observable<VendorPoViewDto> {
    return this.api.get<VendorPoViewDto>(`/api/purchase-orders/${id}/vendor-view`);
  }
}
