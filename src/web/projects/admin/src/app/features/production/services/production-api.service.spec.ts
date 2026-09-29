import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ProductionApiService } from './production-api.service';

describe('ProductionApiService', () => {
  let api: ProductionApiService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    api = TestBed.inject(ProductionApiService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('searches with only the filters that are set, and the page (AC-32)', () => {
    api.search({ statusId: null, search: '', atRisk: false, page: 2, pageSize: 25 }).subscribe();
    const first = http.expectOne((r) => r.url.endsWith('/api/production/runs'));
    expect(first.request.params.keys().sort()).toEqual(['page', 'pageSize']);
    expect(first.request.params.get('page')).toBe('2');
    first.flush({ items: [], page: 2, pageSize: 25, totalCount: 0, totalPages: 0 });

    api.search({ statusId: 1, search: 'PO-1', atRisk: true, page: 1, pageSize: 10 }).subscribe();
    const second = http.expectOne((r) => r.url.endsWith('/api/production/runs'));
    expect(second.request.params.get('statusId')).toBe('1');
    expect(second.request.params.get('search')).toBe('PO-1');
    expect(second.request.params.get('atRisk')).toBe('true');
    second.flush({ items: [], page: 1, pageSize: 10, totalCount: 0, totalPages: 0 });
  });

  it('loads a run by id and by purchase order', () => {
    api.getById(5).subscribe();
    http.expectOne((r) => r.url.endsWith('/api/production/runs/5')).flush({});
    api.getByPo(10).subscribe();
    http.expectOne((r) => r.url.endsWith('/api/production/runs/by-po/10')).flush({});
  });

  it('posts a milestone with the acting person', () => {
    const value = { typeId: 1, claimedDate: '2026-09-03', reportedBy: 'Ali', actingPerson: 'Osama', note: null, finishedLines: null };

    api.recordMilestone(5, value).subscribe();

    const request = http.expectOne((r) => r.url.endsWith('/api/production/runs/5/milestones'));
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual(value);
    request.flush({});
  });

  it('sends the decision to the round route without repeating the round number in the body', () => {
    api.decideSample(5, { roundNo: 2, approve: false, reason: 'neck', actingPerson: 'Osama' }).subscribe();

    const request = http.expectOne((r) => r.url.endsWith('/api/production/runs/5/pp-rounds/2/decision'));
    expect(request.request.body).toEqual({ approve: false, reason: 'neck', actingPerson: 'Osama' });
    request.flush({});
  });

  it('uploads a sample file as multipart and builds a plain download link', () => {
    const file = new File(['png'], 'front.png', { type: 'image/png' });

    api.uploadSampleFile(5, 1, file).subscribe();

    const request = http.expectOne((r) => r.url.endsWith('/api/production/runs/5/pp-rounds/1/files'));
    expect(request.request.body instanceof FormData).toBe(true);
    expect((request.request.body as FormData).get('file')).toBeInstanceOf(File);
    request.flush({});
    expect(api.sampleFileUrl(5, 1, 9)).toMatch(/\/api\/production\/runs\/5\/pp-rounds\/1\/files\/9$/);
  });

  it('puts the expected completion date and posts a delivery note', () => {
    api.setExpectedCompletion(5, { date: '2026-10-20', reason: null, actingPerson: 'Osama' }).subscribe();
    const put = http.expectOne((r) => r.url.endsWith('/api/production/runs/5/expected-completion'));
    expect(put.request.method).toBe('PUT');
    put.flush({});

    api
      .recordDeliveryNote(5, { vendorDnNo: 'DN-1', dispatchDate: '2026-09-25', isFinalShipment: true, lines: [{ sizeId: 1, colourId: 10, qty: 5 }], actingPerson: 'Osama' })
      .subscribe();
    const post = http.expectOne((r) => r.url.endsWith('/api/production/runs/5/delivery-notes'));
    expect(post.request.method).toBe('POST');
    expect((post.request.body as { isFinalShipment: boolean }).isFinalShipment).toBe(true);
    post.flush({});
  });

  it('turns an API refusal into the shared error shape', () => {
    let message = '';
    api.recordMilestone(5, { typeId: 6, claimedDate: '2026-09-03', reportedBy: 'Ali', actingPerson: 'Osama', note: null, finishedLines: null }).subscribe({
      error: (error: { message: string }) => (message = error.message),
    });

    http
      .expectOne((r) => r.url.endsWith('/milestones'))
      .flush({ title: 'The request conflicts.', detail: 'Bulk cutting cannot be recorded until a sample is approved.' }, { status: 409, statusText: 'Conflict' });

    expect(message).toContain('Bulk cutting cannot be recorded');
  });
});
