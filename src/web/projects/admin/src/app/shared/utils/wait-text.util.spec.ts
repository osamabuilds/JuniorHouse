import { waitText } from './wait-text.util';

describe('waitText', () => {
  it.each([
    [1, '1 second'],
    [30, '30 seconds'],
    [59, '59 seconds'],
    [60, '1 minute'],
    [61, '2 minutes'],
    [90, '2 minutes'],
    [900, '15 minutes'],
  ])('says %i seconds as "%s"', (seconds, expected) => {
    expect(waitText(seconds)).toBe(expected);
  });
});
