import { parseTerms, termsToText } from './synonym.form';

describe('synonym form helpers (spec SRC-12)', () => {
  it('splits the words on new lines and commas, trims them and drops empty ones', () => {
    expect(parseTerms('frock\n dress ,gown\r\n\n shalwar kameez ,')).toEqual(['frock', 'dress', 'gown', 'shalwar kameez']);
    expect(parseTerms('   \n ,')).toEqual([]);
  });

  it('shows the words one per line', () => {
    expect(termsToText(['a1', 'b1'])).toBe('a1\nb1');
    expect(parseTerms(termsToText(['x y', 'z']))).toEqual(['x y', 'z']);
  });
});
