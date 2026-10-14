import { TestBed } from '@angular/core/testing';
import { formatDate } from '@shared';
import { LOOKUP_NAMES_STUB } from '@app/testing/lookup-names.stub';
import { deliveryNote, quantities, textOf } from '@app/testing/production-fixtures';
import { DeliveryNoteDto, OrderedVsShippedDto } from '../../models';
import { DeliveryNoteListComponent } from './delivery-note-list.component';

describe('DeliveryNoteListComponent (spec AC-19, AC-21, AC-22, AC-24, AC-33, AC-37)', () => {
  function create(notes: DeliveryNoteDto[], rows: OrderedVsShippedDto[] = quantities()) {
    const fixture = TestBed.createComponent(DeliveryNoteListComponent);
    fixture.componentRef.setInput('notes', notes);
    fixture.componentRef.setInput('quantities', rows);
    fixture.componentRef.setInput('names', LOOKUP_NAMES_STUB);
    fixture.detectChanges();
    return fixture;
  }

  it('says when there are no delivery notes yet', () => {
    expect(textOf(create([]))).toContain('No delivery notes yet');
  });

  it('shows ordered, shipped and still needed per size and colour, against the minimum the terms allow', () => {
    const fixture = create([], [{ sizeId: 1, colourId: 10, ordered: 100, shipped: 60, minRequired: 95, maxAllowed: 105 }]);

    const text = textOf(fixture);

    expect(text).toContain('Size 1');
    expect(text).toContain('100');
    expect(text).toContain('60');
    expect(text).toContain('35'); // still needed: 95 - 60
  });

  it('writes "Over the limit" beside a size shipped beyond the allowance', () => {
    const fixture = create([], [{ sizeId: 1, colourId: 10, ordered: 100, shipped: 110, minRequired: 95, maxAllowed: 105 }]);

    expect(textOf(fixture)).toContain('Over the limit of 105');
  });

  it('shows an on-time full delivery plainly', () => {
    const text = textOf(create([deliveryNote()]));

    expect(text).toContain('Delivery note DN-1');
    expect(text).toContain('On time');
    expect(text).toContain('judged against revision 0');
  });

  it('writes late and beyond-acceptable in words with the number of days (AC-22)', () => {
    const late = textOf(create([deliveryNote({ timingCode: 'Late', daysLate: 2 })]));
    const beyond = textOf(create([deliveryNote({ timingCode: 'BeyondAcceptable', daysLate: 11 })]));

    expect(late).toContain('Late by 2 days');
    expect(beyond).toContain('Beyond the latest acceptable date (11 days late)');
  });

  it('writes over-delivery in words, and says the receiving check decides about the extra (AC-21)', () => {
    const text = textOf(create([deliveryNote({ quantityStatusCode: 'Over', overQty: 1 })]));

    expect(text).toContain('Over by 1 piece');
    expect(text).toContain('the receiving check decides what happens to the extra');
  });

  it('writes a short final shipment in words and marks it final (AC-24)', () => {
    const text = textOf(create([deliveryNote({ isFinalShipment: true, quantityStatusCode: 'Short', shortQty: 23 })]));

    expect(text).toContain('Final shipment');
    expect(text).toContain('Short by 23 pieces');
  });

  it('says a Romp fabric delay is not counted against the vendor (AC-37)', () => {
    const text = textOf(create([deliveryNote({ isRompFabricDelay: true, fabricDispatchDate: '2026-09-14' })]));

    expect(text).toContain('Romp fabric delay');
    expect(text).toContain(`Romp sent the fabric on ${formatDate('2026-09-14')}`);
    expect(text).toContain('not counted against the vendor');
  });

  it('says Romp never recorded sending the fabric when there is no dispatch date', () => {
    const text = textOf(create([deliveryNote({ isRompFabricDelay: true, fabricDispatchDate: null })]));

    expect(text).toContain('Romp never recorded sending the fabric');
  });

  it('is one article per delivery note with its own table of pieces (semantic markup)', () => {
    const root = create([deliveryNote({ id: 1 }), deliveryNote({ id: 2, vendorDnNo: 'DN-2' })]).nativeElement as HTMLElement;

    expect(root.querySelectorAll('article').length).toBe(2);
    expect(root.querySelectorAll('article table caption').length).toBe(2);
  });
});
