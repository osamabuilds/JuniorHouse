import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { QualityApiService } from './quality-api.service';

describe('QualityApiService', () => {
  let api: QualityApiService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    api = TestBed.inject(QualityApiService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  const emptyPage = { items: [], page: 1, pageSize: 10, totalCount: 0, totalPages: 0 };

  it('searches deliveries with only the filters that are set, and the page (AC-42)', () => {
    api.searchDeliveries({ status: null, search: '', page: 2, pageSize: 25 }).subscribe();
    const first = http.expectOne((r) => r.url.endsWith('/api/quality/inbound-deliveries'));
    expect(first.request.params.keys().sort()).toEqual(['page', 'pageSize']);
    first.flush({ ...emptyPage, page: 2, pageSize: 25 });

    api.searchDeliveries({ status: 'open', search: 'DN-1', page: 1, pageSize: 10 }).subscribe();
    const second = http.expectOne((r) => r.url.endsWith('/api/quality/inbound-deliveries'));
    expect(second.request.params.get('status')).toBe('open');
    expect(second.request.params.get('search')).toBe('DN-1');
    second.flush(emptyPage);
  });

  it('loads one delivery and records a goods receipt', () => {
    api.getDelivery(11).subscribe();
    http.expectOne((r) => r.url.endsWith('/api/quality/inbound-deliveries/11')).flush({});

    const value = { inboundDeliveryId: 11, receivedDate: '2026-09-25', actingPerson: 'Sara Khan', note: null, lines: [{ sizeId: 1, colourId: 10, qty: 90 }] };
    api.recordReceipt(value).subscribe();
    const request = http.expectOne((r) => r.url.endsWith('/api/quality/grns'));
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual(value);
    request.flush({});
  });

  it('searches batches, loads one and previews the AQL plan', () => {
    api.searchBatches({ statusId: 2, search: '', page: 1, pageSize: 10 }).subscribe();
    const search = http.expectOne((r) => r.url.endsWith('/api/quality/batches'));
    expect(search.request.params.get('statusId')).toBe('2');
    expect(search.request.params.has('search')).toBe(false);
    search.flush(emptyPage);

    api.getBatch(41).subscribe();
    http.expectOne((r) => r.url.endsWith('/api/quality/batches/41')).flush({});

    api.previewPlan(1000).subscribe();
    const plan = http.expectOne((r) => r.url.endsWith('/api/quality/aql/plan'));
    expect(plan.request.params.get('lotQty')).toBe('1000');
    plan.flush({});
  });

  it('posts an inspection to the batch and puts the safety checks on the inspection', () => {
    const inspection = { subBatchId: null, inspectionDate: '2026-09-26', actingPerson: 'Ali Raza', sampleQty: null, checks: [], defects: [] };
    api.recordInspection(41, inspection).subscribe();
    const post = http.expectOne((r) => r.url.endsWith('/api/quality/batches/41/inspections'));
    expect(post.request.method).toBe('POST');
    expect(post.request.body).toEqual(inspection);
    post.flush({});

    api.recordSafetyChecks({ inspectionId: 51, actingPerson: 'Ali Raza', checks: [] }).subscribe();
    const put = http.expectOne((r) => r.url.endsWith('/api/quality/inspections/51/safety-checks'));
    expect(put.request.method).toBe('PUT');
    expect(put.request.body).toEqual({ actingPerson: 'Ali Raza', checks: [] }); // the id is in the route, not the body
    put.flush({});
  });

  it('records decisions and returns against the right route, without the id in the body', () => {
    api.decideBatch(41, { actingPerson: 'Ali Raza', parts: [{ outcomeId: 1, reason: null, lines: null }] }).subscribe();
    const batch = http.expectOne((r) => r.url.endsWith('/api/quality/batches/41/decision'));
    expect(batch.request.body.parts).toHaveLength(1);
    batch.flush({});

    api.decideSubBatch({ subBatchId: 62, outcomeId: 3, reason: 'still torn', actingPerson: 'Ali Raza' }).subscribe();
    const part = http.expectOne((r) => r.url.endsWith('/api/quality/sub-batches/62/decision'));
    expect(part.request.body).toEqual({ outcomeId: 3, reason: 'still torn', actingPerson: 'Ali Raza' });
    part.flush({});

    api.recordReworkReturn({ subBatchId: 62, returnDate: '2026-09-28', actingPerson: 'Sara Khan', note: null, lines: [{ sizeId: 2, colourId: 10, qty: 30 }] }).subscribe();
    const returned = http.expectOne((r) => r.url.endsWith('/api/quality/sub-batches/62/rework-return'));
    expect(returned.request.body.lines).toEqual([{ sizeId: 2, colourId: 10, qty: 30 }]);
    returned.flush({});
  });
});
