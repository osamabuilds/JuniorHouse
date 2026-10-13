import {
  FABRIC_ROMP_SUPPLIED,
  FABRIC_VENDOR_SUPPLIED,
  MILESTONE_BULK_CUTTING_STARTED,
  MILESTONE_FABRIC_BOOKED,
  MILESTONE_FABRIC_DISPATCHED_BY_ROMP,
  MILESTONE_FINISHED_READY_TO_SHIP,
  MILESTONE_LABELS,
} from '../production-runs.constants';
import { RunDetailDto, RunSummaryDto } from '../models';

/** Staff enter Pakistan-time dates (UTC+5, no daylight saving), so "today" for a date box is that day, whatever the browser's own zone. */
export function todayInPakistan(now: Date = new Date()): string {
  return new Date(now.getTime() + 5 * 60 * 60 * 1000).toISOString().slice(0, 10);
}

export interface MilestoneChoice {
  readonly id: number;
  readonly label: string;
  /** Shown as the reason a choice is listed but cannot be picked yet. */
  readonly disabledReason: string | null;
}

/**
 * The milestones staff can record by hand on this run (D10): fabric booked only when the vendor supplies
 * the fabric, fabric dispatch only when Romp does, bulk cutting until it has started (and only once a sample
 * is approved), and finished quantity once cutting has started. Sample steps have their own panel.
 */
export function milestoneChoices(run: Pick<RunDetailDto, 'fabricResponsibilityId' | 'hasApprovedSample' | 'cuttingStartedDate'>): MilestoneChoice[] {
  const choices: MilestoneChoice[] = [];

  if (run.fabricResponsibilityId === FABRIC_VENDOR_SUPPLIED) {
    choices.push({ id: MILESTONE_FABRIC_BOOKED, label: MILESTONE_LABELS[MILESTONE_FABRIC_BOOKED], disabledReason: null });
  }

  if (run.fabricResponsibilityId === FABRIC_ROMP_SUPPLIED) {
    choices.push({ id: MILESTONE_FABRIC_DISPATCHED_BY_ROMP, label: MILESTONE_LABELS[MILESTONE_FABRIC_DISPATCHED_BY_ROMP], disabledReason: null });
  }

  if (!run.cuttingStartedDate) {
    choices.push({
      id: MILESTONE_BULK_CUTTING_STARTED,
      label: MILESTONE_LABELS[MILESTONE_BULK_CUTTING_STARTED],
      disabledReason: run.hasApprovedSample ? null : 'needs an approved pre-production sample first',
    });
  }

  if (run.cuttingStartedDate) {
    choices.push({ id: MILESTONE_FINISHED_READY_TO_SHIP, label: MILESTONE_LABELS[MILESTONE_FINISHED_READY_TO_SHIP], disabledReason: null });
  }

  return choices;
}

export type FlagTone = 'danger' | 'warn' | 'info';

/** A warning shown as words (never colour alone) beside a run. */
export interface RunFlag {
  readonly code: string;
  readonly label: string;
  readonly tone: FlagTone;
}

type FlagSource = Pick<
  RunSummaryDto,
  'isAtRisk' | 'isCuttingBeforeFabricDispatch' | 'isResampleRecommended' | 'isCuttingWithoutResample'
> &
  Partial<Pick<RunSummaryDto, 'hasOverDelivery' | 'hasShortDelivery' | 'hasLateDelivery'>>;

export function runFlags(run: FlagSource): RunFlag[] {
  const flags: RunFlag[] = [];

  if (run.isAtRisk) {
    flags.push({ code: 'at-risk', label: 'At risk of being late', tone: 'danger' });
  }
  if (run.isCuttingBeforeFabricDispatch) {
    flags.push({ code: 'cut-before-fabric', label: 'Cutting started before Romp sent the fabric', tone: 'danger' });
  }
  if (run.isCuttingWithoutResample) {
    flags.push({ code: 'cut-without-resample', label: 'Cutting started without a new sample after a construction change', tone: 'danger' });
  } else if (run.isResampleRecommended) {
    flags.push({ code: 'resample', label: 'A new sample is strongly recommended', tone: 'warn' });
  }
  if (run.hasOverDelivery) {
    flags.push({ code: 'over', label: 'Delivered more than allowed', tone: 'danger' });
  }
  if (run.hasShortDelivery) {
    flags.push({ code: 'short', label: 'Delivered short', tone: 'danger' });
  }
  if (run.hasLateDelivery) {
    flags.push({ code: 'late', label: 'Delivered late', tone: 'warn' });
  }

  return flags;
}
