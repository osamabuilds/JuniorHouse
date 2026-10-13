/** PROD.MLST_TYP_LKP ids (system-owned, stable). */
export const MILESTONE_FABRIC_BOOKED = 1;
export const MILESTONE_FABRIC_DISPATCHED_BY_ROMP = 2;
export const MILESTONE_PP_SAMPLE_SUBMITTED = 3;
export const MILESTONE_PP_SAMPLE_APPROVED = 4;
export const MILESTONE_PP_SAMPLE_REJECTED = 5;
export const MILESTONE_BULK_CUTTING_STARTED = 6;
export const MILESTONE_FINISHED_READY_TO_SHIP = 7;

/** Plain-language names for the timeline. */
export const MILESTONE_LABELS: Readonly<Record<number, string>> = {
  1: 'Fabric booked by the vendor',
  2: 'Fabric dispatched by Romp',
  3: 'Pre-production sample submitted',
  4: 'Pre-production sample approved',
  5: 'Pre-production sample rejected',
  6: 'Bulk cutting started',
  7: 'Finished and ready to ship',
};

/** PROD.PROD_RUN_STS_LKP ids. */
export const RUN_STATUS_OPEN = 1;
export const RUN_STATUS_CANCELLED = 2;
export const RUN_STATUS_CLOSED = 3;

export const RUN_STATUS_LABELS: Readonly<Record<number, string>> = {
  1: 'Open',
  2: 'Cancelled',
  3: 'Closed',
};

/** REF.FBRC_RESP_LKP ids the run screens depend on. */
export const FABRIC_VENDOR_SUPPLIED = 1;
export const FABRIC_ROMP_SUPPLIED = 2;

/** The key the acting person's name is remembered under in this browser (D13, until staff login exists in S8). */
export const ACTING_PERSON_STORAGE_KEY = 'romp.admin.actingPerson';
