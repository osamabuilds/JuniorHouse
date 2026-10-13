import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiClient } from '@core/http';
import { Page } from '@shared';
import {
  AqlPlanDto,
  BatchDecisionDto,
  BatchFilters,
  DecideBatchValue,
  DecideSubBatchValue,
  DeliveryFilters,
  GoodsReceiptDto,
  InboundDeliveryDto,
  InboundDeliverySummaryDto,
  InspectionDto,
  QcBatchDto,
  QcBatchSummaryDto,
  RecordInspectionValue,
  RecordReceiptValue,
  RecordSafetyValue,
  ReworkReturnResultDto,
  ReworkReturnValue,
} from '../models';

/** HTTP only, no state: the Quality API (`/api/quality/...`). */
@Injectable({ providedIn: 'root' })
export class QualityApiService {
  private readonly api = inject(ApiClient);

  searchDeliveries(filters: DeliveryFilters): Observable<Page<InboundDeliverySummaryDto>> {
    return this.api.get<Page<InboundDeliverySummaryDto>>('/api/quality/inbound-deliveries', {
      status: filters.status ?? undefined,
      search: filters.search || undefined,
      page: filters.page,
      pageSize: filters.pageSize,
    });
  }

  getDelivery(id: number): Observable<InboundDeliveryDto> {
    return this.api.get<InboundDeliveryDto>(`/api/quality/inbound-deliveries/${id}`);
  }

  recordReceipt(value: RecordReceiptValue): Observable<GoodsReceiptDto> {
    return this.api.post<GoodsReceiptDto>('/api/quality/grns', value);
  }

  searchBatches(filters: BatchFilters): Observable<Page<QcBatchSummaryDto>> {
    return this.api.get<Page<QcBatchSummaryDto>>('/api/quality/batches', {
      statusId: filters.statusId ?? undefined,
      search: filters.search || undefined,
      page: filters.page,
      pageSize: filters.pageSize,
    });
  }

  getBatch(id: number): Observable<QcBatchDto> {
    return this.api.get<QcBatchDto>(`/api/quality/batches/${id}`);
  }

  /** What the AQL tables will apply to a lot (the same calculator the inspection uses). */
  previewPlan(lotQty: number): Observable<AqlPlanDto> {
    return this.api.get<AqlPlanDto>('/api/quality/aql/plan', { lotQty });
  }

  recordInspection(batchId: number, value: RecordInspectionValue): Observable<InspectionDto> {
    return this.api.post<InspectionDto>(`/api/quality/batches/${batchId}/inspections`, value);
  }

  recordSafetyChecks(value: RecordSafetyValue): Observable<InspectionDto> {
    const { inspectionId, ...body } = value;
    return this.api.put<InspectionDto>(`/api/quality/inspections/${inspectionId}/safety-checks`, body);
  }

  decideBatch(batchId: number, value: DecideBatchValue): Observable<BatchDecisionDto> {
    return this.api.post<BatchDecisionDto>(`/api/quality/batches/${batchId}/decision`, value);
  }

  decideSubBatch(value: DecideSubBatchValue): Observable<BatchDecisionDto> {
    const { subBatchId, ...body } = value;
    return this.api.post<BatchDecisionDto>(`/api/quality/sub-batches/${subBatchId}/decision`, body);
  }

  recordReworkReturn(value: ReworkReturnValue): Observable<ReworkReturnResultDto> {
    const { subBatchId, ...body } = value;
    return this.api.post<ReworkReturnResultDto>(`/api/quality/sub-batches/${subBatchId}/rework-return`, body);
  }
}
