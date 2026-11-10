import { outcomeWords, parseStatementText } from './statement-text';

describe('statement text (spec COD-16)', () => {
  it('reads waybill and amount separated by a comma, a tab or spaces, and totals them', () => {
    const parsed = parseStatementText('prm-1, 4,700\nPRM-2\t1500.50\n\n PRM-3   300 ');

    expect(parsed.problems).toEqual([]);
    expect(parsed.lines.map((l) => l.waybill)).toEqual(['PRM-1', 'PRM-2', 'PRM-3']);
    expect(parsed.lines.map((l) => l.amount)).toEqual([4700, 1500.5, 300]);
    expect(parsed.total).toBe(6500.5);
  });

  it('names the row that has no amount, a zero amount or too many decimals', () => {
    const parsed = parseStatementText('PRM-1\nPRM-2, 0\nPRM-3, 10.123\nPRM-4, 5');

    expect(parsed.problems.length).toBe(3);
    expect(parsed.problems[0]).toContain('Row 1');
    expect(parsed.lines).toEqual([{ waybill: 'PRM-4', amount: 5 }]);
  });

  it('says every outcome in words', () => {
    expect(outcomeWords('MATCHED')).toBe('Matched');
    expect(outcomeWords('SHORT')).toContain('Short');
    expect(outcomeWords('NOT_COD')).toContain('Not a cash parcel');
  });
});
