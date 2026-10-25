import { parseSelection, productKey, selectionQuery, selectionQueryString } from './product-selection';

describe('product selection', () => {
  it('reads a colour and size, lower-cased and trimmed', () => {
    expect(parseSelection({ colour: ' Navy ', size: '5-6Y' })).toEqual({ colour: 'navy', size: '5-6y' });
  });

  it('drops anything that is not a plausible code', () => {
    expect(parseSelection({ colour: '<script>', size: 'a'.repeat(41) })).toEqual({ colour: null, size: null });
    expect(parseSelection({ colour: '', size: undefined })).toEqual({ colour: null, size: null });
    expect(parseSelection({ colour: 'navy blue', size: '../x' })).toEqual({ colour: null, size: null });
  });

  it('spells a selection one way, colour first, and leaves out what is not chosen', () => {
    expect(selectionQuery({ colour: 'navy', size: '5-6y' })).toEqual({ colour: 'navy', size: '5-6y' });
    expect(selectionQueryString({ colour: null, size: '5-6y' })).toBe('size=5-6y');
    expect(selectionQueryString({ colour: null, size: null })).toBe('');
  });

  it('keys a request by path and choice', () => {
    expect(productKey('/girls/dresses/floral', { colour: 'navy', size: null })).toBe('/girls/dresses/floral?colour=navy');
  });
});
