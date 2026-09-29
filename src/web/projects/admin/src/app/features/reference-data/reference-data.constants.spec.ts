import { LOOKUP_TYPES } from './reference-data.constants';

describe('LOOKUP_TYPES (SCRUM-93 task 54)', () => {
  const byKey = (key: string) => LOOKUP_TYPES.find((type) => type.key === key);

  it('offers the two staff-maintained Sprint 2 lookups for editing', () => {
    expect(byKey('amendment-reasons')?.mutable).toBe(true);
    expect(byKey('vendor-comm-channels')?.mutable).toBe(true);
  });

  it('does not list the system-owned Sprint 2 lookups', () => {
    for (const key of ['revision-statuses', 'amendment-initiators', 'vendor-comm-types', 'po-file-categories', 'fabric-responsibilities']) {
      expect(byKey(key)).toBeUndefined();
    }
  });

  it('keeps PO Statuses read-only', () => {
    expect(byKey('po-statuses')?.mutable).toBe(false);
  });
});
