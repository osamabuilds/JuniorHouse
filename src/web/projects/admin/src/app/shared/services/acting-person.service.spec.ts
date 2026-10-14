import { TestBed } from '@angular/core/testing';
import { ActingPersonService } from './acting-person.service';

describe('ActingPersonService (spec D13)', () => {
  beforeEach(() => {
    localStorage.clear();
    TestBed.resetTestingModule();
  });

  it('starts empty when this browser has never been used to record anything', () => {
    expect(TestBed.inject(ActingPersonService).name()).toBe('');
  });

  it('remembers the trimmed name for next time, across a new service instance', () => {
    TestBed.inject(ActingPersonService).remember('  Osama Shafique ');

    TestBed.resetTestingModule();

    expect(TestBed.inject(ActingPersonService).name()).toBe('Osama Shafique');
  });

  it('ignores a blank name rather than forgetting the last one', () => {
    const service = TestBed.inject(ActingPersonService);
    service.remember('Osama Shafique');

    service.remember('   ');

    expect(service.name()).toBe('Osama Shafique');
  });

  it('still works for this session when browser storage is unavailable', () => {
    const setItem = vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new Error('blocked');
    });
    const service = TestBed.inject(ActingPersonService);

    service.remember('Osama Shafique');

    expect(service.name()).toBe('Osama Shafique');
    setItem.mockRestore();
  });
});
