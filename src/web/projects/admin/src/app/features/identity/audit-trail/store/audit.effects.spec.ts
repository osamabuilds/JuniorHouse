import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideEffects } from '@ngrx/effects';
import { Store, provideStore } from '@ngrx/store';
import { EMPTY_AUDIT_FORM } from '../models';
import { AuditPageActions } from './audit.actions';
import * as effects from './audit.effects';
import { auditReducer } from './audit.reducer';
import { selectAuditEntries, selectAuditError, selectAuditLoading, selectAuditPage, selectAuditSelected, selectAuditTotal } from './audit.selectors';
import { AUDIT_FEATURE_KEY } from './audit.state';

const ENTRY = {
  id: 1,
  occurredAt: '2026-10-26T09:00:00Z',
  module: 'CTLG',
  entityType: 'STYL_MAIN',
  entityKey: '1',
  operation: 'INSERT',
  actor: 'system',
  correlationId: null,
  columns: ['NAME'],
} as const;

const PAGE = { items: [ENTRY], page: 1, pageSize: 25, totalCount: 1, totalPages: 1 };

describe('audit store (audit-trail AC-8, AC-9)', () => {
  let store: Store;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideStore({ [AUDIT_FEATURE_KEY]: auditReducer }), provideEffects(effects)],
    });
    store = TestBed.inject(Store);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('opening the page loads the newest entries', () => {
    store.dispatch(AuditPageActions.opened());
    expect(store.selectSignal(selectAuditLoading)()).toBe(true);

    const request = http.expectOne((r) => r.url.endsWith('/api/identity/audit'));
    expect(request.request.params.get('page')).toBe('1');
    request.flush(PAGE);

    expect(store.selectSignal(selectAuditEntries)()).toEqual([ENTRY]);
    expect(store.selectSignal(selectAuditTotal)()).toBe(1);
    expect(store.selectSignal(selectAuditLoading)()).toBe(false);
  });

  it('applying filters goes back to page one and sends them, with the day turned into a range', () => {
    store.dispatch(AuditPageActions.pageChanged({ page: 3 }));
    http.expectOne((r) => r.url.endsWith('/api/identity/audit')).flush({ ...PAGE, page: 3 });

    store.dispatch(AuditPageActions.filtersApplied({ form: { ...EMPTY_AUDIT_FORM, actor: 'amna.khan', operation: 'UPDATE', fromDate: '2026-10-01', toDate: '2026-10-26' } }));
    const request = http.expectOne((r) => r.url.endsWith('/api/identity/audit'));

    expect(request.request.params.get('actor')).toBe('amna.khan');
    expect(request.request.params.get('operation')).toBe('UPDATE');
    expect(request.request.params.get('from')).toBe('2026-10-01T00:00:00Z');
    expect(request.request.params.get('to')).toBe('2026-10-26T23:59:59Z');
    expect(request.request.params.get('page')).toBe('1');
    expect(request.request.params.has('module')).toBe(false);
    request.flush(PAGE);
    expect(store.selectSignal(selectAuditPage)()).toBe(1);
  });

  it('a failure shows the reason and no entries', () => {
    store.dispatch(AuditPageActions.opened());
    http.expectOne((r) => r.url.endsWith('/api/identity/audit')).flush({}, { status: 403, statusText: 'Forbidden' });

    expect(store.selectSignal(selectAuditEntries)()).toEqual([]);
    expect(store.selectSignal(selectAuditError)()?.status).toBe(403);
  });

  it('opening and closing an entry needs no request', () => {
    store.dispatch(AuditPageActions.entrySelected({ entry: ENTRY }));
    expect(store.selectSignal(selectAuditSelected)()).toEqual(ENTRY);

    store.dispatch(AuditPageActions.detailClosed());
    expect(store.selectSignal(selectAuditSelected)()).toBeNull();
  });
});
