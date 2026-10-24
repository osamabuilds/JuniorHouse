import { draftSettings } from '@app/testing/storefront-fixtures';
import { blankRule, parseAmount, toDeliveryValues, toExchange } from './delivery.util';

describe('delivery values (spec ST-7, ST-4)', () => {
  it('reads the rules and the exchange policy out of the draft', () => {
    const values = toDeliveryValues(draftSettings());

    expect(values.rules).toHaveLength(1);
    expect(values).toMatchObject({ windowDays: 7, conditions: ['Unworn.'] });
  });

  it('starts blank when there is no exchange policy yet', () => {
    expect(toDeliveryValues(draftSettings({ exchange: null }))).toMatchObject({ windowDays: null, conditions: [] });
    expect(toDeliveryValues(null).rules).toEqual([]);
  });

  it('saves no policy at all when neither a window nor a condition was entered', () => {
    expect(toExchange({ rules: [], windowDays: null, conditions: ['', '  '] })).toBeNull();
  });

  it('saves the window and the non-empty conditions', () => {
    expect(toExchange({ rules: [], windowDays: 7, conditions: [' Unworn. ', ''] })).toEqual({ windowDays: 7, conditions: ['Unworn.'] });
    expect(toExchange({ rules: [], windowDays: null, conditions: ['Tags on.'] })).toEqual({ windowDays: 0, conditions: ['Tags on.'] });
  });

  it('reads a number box: blank is not set, text that is not a number is not set either', () => {
    expect(parseAmount('')).toBeNull();
    expect(parseAmount(' 250.50 ')).toBe(250.5);
    expect(parseAmount('abc')).toBeNull();
    expect(parseAmount('0')).toBe(0);
  });

  it('a new city row starts with a sensible shape', () => {
    expect(blankRule()).toMatchObject({ city: '', enabled: true, freeOver: null });
  });
});
