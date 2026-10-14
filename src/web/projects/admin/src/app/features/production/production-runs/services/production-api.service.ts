import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiClient } from '@core/http';
import { Page } from '@shared';
import {
  DecideSampleValue,
  DeliveryNoteDto,
  DeliveryNoteValue,
  ExpectedCompletionChangeDto,
  ExpectedCompletionValue,
  MilestoneDto,
  PpSampleFileDto,
  PpSampleRoundDto,
  RecordMilestoneValue,
  RunDetailDto,
  RunFilters,
  RunSummaryDto,
  SubmitSampleValue,
} from '../models';

@Injectable({ providedIn: 'root' })
export class ProductionApiService {
  private readonly api = inject(ApiClient);

  search(filters: RunFilters): Observable<Page<RunSummaryDto>> {
    return this.api.get<Page<RunSummaryDto>>('/api/production/runs', {
      statusId: filters.statusId ?? undefined,
      search: filters.search || undefined,
      atRisk: filters.atRisk ? true : undefined,
      page: filters.page,
      pageSize: filters.pageSize,
    });
  }

  getById(id: number): Observable<RunDetailDto> {
    return this.api.get<RunDetailDto>(`/api/production/runs/${id}`);
  }

  /** The PO's production run; the API answers 404 while the PO has none (not yet acknowledged). */
  getByPo(poId: number): Observable<RunDetailDto> {
    return this.api.get<RunDetailDto>(`/api/production/runs/by-po/${poId}`);
  }

  recordMilestone(runId: number, value: RecordMilestoneValue): Observable<MilestoneDto> {
    return this.api.post<MilestoneDto>(`/api/production/runs/${runId}/milestones`, value);
  }

  submitSample(runId: number, value: SubmitSampleValue): Observable<PpSampleRoundDto> {
    return this.api.post<PpSampleRoundDto>(`/api/production/runs/${runId}/pp-rounds`, value);
  }

  decideSample(runId: number, value: DecideSampleValue): Observable<PpSampleRoundDto> {
    const { roundNo, ...body } = value;
    return this.api.post<PpSampleRoundDto>(`/api/production/runs/${runId}/pp-rounds/${roundNo}/decision`, body);
  }

  uploadSampleFile(runId: number, roundNo: number, file: File): Observable<PpSampleFileDto> {
    const body = new FormData();
    body.append('file', file, file.name);
    return this.api.postForm<PpSampleFileDto>(`/api/production/runs/${runId}/pp-rounds/${roundNo}/files`, body);
  }

  removeSampleFile(runId: number, roundNo: number, fileId: number): Observable<void> {
    return this.api.delete<void>(`/api/production/runs/${runId}/pp-rounds/${roundNo}/files/${fileId}`);
  }

  /** A plain download link (the API forces a download and sends nosniff). */
  sampleFileUrl(runId: number, roundNo: number, fileId: number): string {
    return this.api.url(`/api/production/runs/${runId}/pp-rounds/${roundNo}/files/${fileId}`);
  }

  setExpectedCompletion(runId: number, value: ExpectedCompletionValue): Observable<ExpectedCompletionChangeDto> {
    return this.api.put<ExpectedCompletionChangeDto>(`/api/production/runs/${runId}/expected-completion`, value);
  }

  recordDeliveryNote(runId: number, value: DeliveryNoteValue): Observable<DeliveryNoteDto> {
    return this.api.post<DeliveryNoteDto>(`/api/production/runs/${runId}/delivery-notes`, value);
  }
}
