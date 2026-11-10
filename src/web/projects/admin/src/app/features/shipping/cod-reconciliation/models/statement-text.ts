import { StatementLineInput } from './cod.model';

export interface ParsedStatement {
  readonly lines: readonly StatementLineInput[];
  /** Plain messages naming the bad row; empty when every row is usable (COD-16). */
  readonly problems: readonly string[];
  readonly total: number;
}

function hasAtMostTwoDecimals(amount: number): boolean {
  return Math.abs(Math.round(amount * 100) - amount * 100) < 1e-6;
}

/**
 * Turns the text finance pastes from the courier statement into lines: one parcel per row, "waybill, amount" separated by a comma, a tab or spaces.
 * Thousands separators in the amount are dropped. The server checks everything again; this only saves a round trip (COD-16).
 */
export function parseStatementText(text: string): ParsedStatement {
  const lines: StatementLineInput[] = [];
  const problems: string[] = [];
  text.split(/\r?\n/).forEach((raw, index) => {
    const row = raw.trim();
    if (row.length === 0) {
      return;
    }

    const parts = row.split(/[\s,;]+/).filter((p) => p.length > 0);
    const amount = Number(parts.slice(1).join(''));
    if (parts.length < 2 || !Number.isFinite(amount) || amount <= 0 || !hasAtMostTwoDecimals(amount)) {
      problems.push(`Row ${index + 1}: write the waybill, then an amount above zero with at most two decimals (for example "PRM-000123, 4700").`);
      return;
    }

    lines.push({ waybill: parts[0].toUpperCase(), amount });
  });

  const total = Math.round(lines.reduce((sum, l) => sum + l.amount, 0) * 100) / 100;
  return { lines, problems, total };
}

/** The outcome in words, never colour alone (COD-3). */
export function outcomeWords(outcome: string): string {
  switch (outcome) {
    case 'MATCHED':
      return 'Matched';
    case 'SHORT':
      return 'Short: less cash than the parcel needed';
    case 'OVER':
      return 'Over: more cash than the parcel needed';
    case 'DUPLICATE':
      return 'Paid before';
    case 'UNKNOWN':
      return 'Unknown waybill';
    case 'NOT_DELIVERED':
      return 'Not delivered yet';
    default:
      return 'Not a cash parcel';
  }
}
