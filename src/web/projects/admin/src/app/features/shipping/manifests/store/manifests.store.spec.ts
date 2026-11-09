import { ManifestDetail, ManifestSummary, differenceText, statusWords } from '../models';
import { ManifestDetailActions, ManifestsApiActions, ManifestsPageActions, initialManifestsState, manifestsReducer } from './manifests.store';

const summary: ManifestSummary = { id: 4, courierName: 'Primary', businessDate: '2026-11-10', status: 'OPEN', parcelCount: 5, countedParcels: null, countedBy: null, countedAt: null };
const detail: ManifestDetail = { summary, note: null, totalCash: 2500, totalWeightGrams: 3000, lines: [] };

describe('manifests (spec MNF-1 to MNF-10)', () => {
  it('MNF-10 a loaded page fills the list and the total', () => {
    const state = manifestsReducer(initialManifestsState, ManifestsApiActions.listSucceeded({ result: { items: [summary], page: 1, pageSize: 10, totalCount: 1, totalPages: 1 } }));

    expect(state.rows).toEqual([summary]);
    expect(state.total).toBe(1);
  });

  it('MNF-4 generating says how many were made and frees the button', () => {
    const waiting = manifestsReducer(initialManifestsState, ManifestsPageActions.generateClicked());
    const done = manifestsReducer(waiting, ManifestsApiActions.generateSucceeded({ count: 2 }));

    expect(waiting.busy).toBe(true);
    expect(done.busy).toBe(false);
    expect(done.notice).toContain('2 manifests');
  });

  it('MNF-5 a short count reads as a mismatch with the difference', () => {
    const counted: ManifestSummary = { ...summary, status: 'MISMATCH', countedParcels: 3 };

    expect(differenceText(counted)).toBe('2 parcels short');
    expect(statusWords(counted)).toBe('Mismatch: 2 parcels short');
    expect(differenceText({ ...counted, countedParcels: 6 })).toBe('1 parcel more than listed');
    expect(statusWords({ ...summary, status: 'MATCHED', countedParcels: 5 })).toContain('Matched');
  });

  it('MNF-6 toggling a parcel makes the page wait, and the fresh detail frees it', () => {
    const waiting = manifestsReducer(initialManifestsState, ManifestDetailActions.handoverToggled({ id: 4, shipmentId: 9, handedOver: false }));
    const done = manifestsReducer(waiting, ManifestsApiActions.detailSucceeded({ detail }));

    expect(waiting.busy).toBe(true);
    expect(done.busy).toBe(false);
    expect(done.detail).toEqual(detail);
  });

  it('MNF-7 a refused change shows the reason and frees the page', () => {
    const waiting = manifestsReducer(initialManifestsState, ManifestDetailActions.countEntered({ id: 4, parcels: 5 }));
    const failed = manifestsReducer(waiting, ManifestsApiActions.requestFailed({ error: { status: 409, message: 'A closed manifest cannot be changed.', fieldErrors: {} } }));

    expect(failed.busy).toBe(false);
    expect(failed.error?.message).toContain('closed');
  });
});
