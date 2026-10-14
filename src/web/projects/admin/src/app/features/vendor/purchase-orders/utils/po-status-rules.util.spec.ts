import { PoDto } from '../models';
import {
  canAmend,
  canCancel,
  canClose,
  canEdit,
  canRecordResponse,
  canSend,
  cancelNeedsProductionWarning,
  isInProductionStage,
  statusHelp,
} from './po-status-rules.util';

const po = (statusId: number) => ({ statusId }) as PoDto;
const DRAFT = 1;
const SENT = 2;
const ACKNOWLEDGED = 3;
const CANCELLED = 4;
const IN_PRODUCTION = 5;
const PARTIALLY_DELIVERED = 6;
const DELIVERED = 7;
const CLOSED = 8;

describe('PO status rules after Sprint 3 (spec AC-25, AC-26, AC-28)', () => {
  it.each([
    [DRAFT, false],
    [SENT, false],
    [ACKNOWLEDGED, true],
    [CANCELLED, false],
    [IN_PRODUCTION, true],
    [PARTIALLY_DELIVERED, true],
    [DELIVERED, false],
    [CLOSED, false],
  ])('status %i: production stage is %s', (status, expected) => {
    expect(isInProductionStage(po(status))).toBe(expected);
  });

  it.each([
    [DRAFT, false],
    [SENT, true],
    [ACKNOWLEDGED, true],
    [IN_PRODUCTION, true],
    [PARTIALLY_DELIVERED, true],
    [DELIVERED, false],
    [CLOSED, false],
    [CANCELLED, false],
  ])('status %i: can amend is %s (AC-26: never once Delivered or Closed)', (status, expected) => {
    expect(canAmend(po(status))).toBe(expected);
  });

  it.each([
    [DRAFT, true],
    [SENT, true],
    [ACKNOWLEDGED, true],
    [IN_PRODUCTION, true],
    [PARTIALLY_DELIVERED, true],
    [DELIVERED, false],
    [CLOSED, false],
    [CANCELLED, false],
  ])('status %i: can cancel is %s (AC-28: allowed after production started)', (status, expected) => {
    expect(canCancel(po(status))).toBe(expected);
  });

  it('closing is offered only for a Delivered order (AC-25)', () => {
    expect([DRAFT, SENT, ACKNOWLEDGED, IN_PRODUCTION, PARTIALLY_DELIVERED, CLOSED, CANCELLED].some((status) => canClose(po(status)))).toBe(false);
    expect(canClose(po(DELIVERED))).toBe(true);
  });

  it('the sunk-cost warning applies once production has started, not before (AC-28)', () => {
    expect(cancelNeedsProductionWarning(po(ACKNOWLEDGED))).toBe(false);
    expect(cancelNeedsProductionWarning(po(IN_PRODUCTION))).toBe(true);
    expect(cancelNeedsProductionWarning(po(PARTIALLY_DELIVERED))).toBe(true);
  });

  it('keeps the Sprint 1 rules for editing, sending and recording a vendor response', () => {
    expect(canEdit(po(DRAFT))).toBe(true);
    expect(canSend(po(DRAFT))).toBe(true);
    expect(canRecordResponse(po(SENT))).toBe(true);
    expect(canRecordResponse(po(ACKNOWLEDGED))).toBe(false);
  });

  it('gives a plain sentence for every status, and says closing is a manual decision', () => {
    for (const status of [DRAFT, SENT, ACKNOWLEDGED, CANCELLED, IN_PRODUCTION, PARTIALLY_DELIVERED, DELIVERED, CLOSED]) {
      expect(statusHelp(po(status)).length).toBeGreaterThan(20);
    }
    expect(statusHelp(po(DELIVERED))).toContain('Closing is always your decision');
    expect(statusHelp(po(CLOSED))).toContain('closed');
    expect(statusHelp(po(CANCELLED))).toContain('cancelled');
  });
});
