import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { LOOKUP_NAMES_STUB } from '@app/testing/lookup-names.stub';
import { buttonByText, textOf } from '@app/testing/production-fixtures';
import { PoDto } from '../../models';
import { PoDetailComponent } from './po-detail.component';
import { provideAllowAllPermissions } from '@app/testing/provide-allow-all-permissions';

const po = (statusId: number): PoDto =>
  ({
    id: 10,
    poNo: 'PO-2026-00010',
    vendorId: 1,
    styleId: 5,
    unitCost: 500,
    expectedDeliveryDate: '2026-12-01',
    paymentTermId: 1,
    advancePercent: 40,
    latestAcceptableDate: '2026-12-15',
    overTolerancePercent: 5,
    underTolerancePercent: 5,
    fabricResponsibilityId: 1,
    statusId,
    lines: [{ sizeId: 1, colourId: 10, qty: 100 }],
    statusHistory: [{ poStatusId: statusId, cancelReasonId: null, insrDte: '2026-09-29T08:00:00Z', insrBy: 'system' }],
  }) as unknown as PoDto;

const reasons = [
  { id: 6, code: 'Other', name: 'Other' },
  { id: 7, code: 'ProductionAlreadyStarted', name: 'Cancelled after production started' },
];

describe('PoDetailComponent after Sprint 3 (spec AC-25, AC-28, D5)', () => {
  function create(statusId: number, extra: Record<string, unknown> = {}) {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({ providers: [provideAllowAllPermissions(), provideRouter([]), provideHttpClient(), provideHttpClientTesting()] });
    const fixture = TestBed.createComponent(PoDetailComponent);
    const inputs: Record<string, unknown> = {
      po: po(statusId),
      names: LOOKUP_NAMES_STUB,
      vendors: [],
      error: null,
      panel: 'none',
      panelSaving: false,
      panelError: null,
      hasTechPack: true,
      currentRevisionNumber: 0,
      pendingRevision: null,
      revisions: [],
      files: [],
      styleCells: [],
      channels: [],
      amendmentReasons: [],
      fabricOptions: [],
      cancelReasons: reasons,
      cancelReasonId: null,
      fileUploading: false,
      fileError: null,
      fileUrlFor: () => '',
      ...extra,
    };
    for (const [key, value] of Object.entries(inputs)) {
      fixture.componentRef.setInput(key, value);
    }
    const closeRequested = vi.fn();
    const cancelRequested = vi.fn();
    fixture.componentInstance.closeRequested.subscribe(closeRequested);
    fixture.componentInstance.cancelRequested.subscribe(cancelRequested);
    fixture.detectChanges();
    return { fixture, root: fixture.nativeElement as HTMLElement, closeRequested, cancelRequested };
  }

  const hasButton = (root: HTMLElement, text: string) => Array.from(root.querySelectorAll('button')).some((button) => (button.textContent ?? '').includes(text));

  it('shows the production statuses by name', () => {
    expect(textOf(create(5).fixture)).toContain('In Production');
    expect(textOf(create(6).fixture)).toContain('Partially Delivered');
    expect(textOf(create(7).fixture)).toContain('Delivered');
    expect(textOf(create(8).fixture)).toContain('Closed');
  });

  it('offers Close only when the order is Delivered, and asks the parent to close it (AC-25)', () => {
    expect(hasButton(create(6).root, 'Close order')).toBe(false);
    expect(hasButton(create(8).root, 'Close order')).toBe(false);

    const { root, closeRequested } = create(7);
    buttonByText(root, 'Close order').click();

    expect(closeRequested).toHaveBeenCalledOnce();
  });

  it('links to the production run once there is one, and not before', () => {
    const withRun = create(5, { productionRunId: 42 }).root;
    const withoutRun = create(2).root;

    expect(withRun.querySelector('a[href="/production/42"]')).not.toBeNull();
    expect(withoutRun.querySelector('a[href^="/production/"]')).toBeNull();
  });

  it('shows no cancel panel for a Delivered or Closed order (AC-28)', () => {
    expect(create(7).root.querySelector('.cancel-panel')).toBeNull();
    expect(create(8).root.querySelector('.cancel-panel')).toBeNull();
  });

  it('offers the production-stage reason in the cancel list', () => {
    const { root } = create(5);

    const options = Array.from(root.querySelectorAll('#cancel-reason option')).map((option) => (option.textContent ?? '').trim());

    expect(options).toContain('Cancelled after production started');
  });

  it('Cancel_InProduction_ShowsWarning: a prominent sunk-cost warning, and the button waits for the person to confirm they understand (D5)', () => {
    const { fixture, root, cancelRequested } = create(5, { cancelReasonId: 7 });

    expect(textOf(fixture)).toContain('Production has already started');
    expect(textOf(fixture)).toContain('The vendor may have bought fabric');
    expect(root.querySelector('.cancel-panel .banner--error')?.getAttribute('role')).toBe('alert');
    const cancel = buttonByText(root, 'Cancel PO');
    expect(cancel.disabled).toBe(true);

    const understood = root.querySelector('#cancel-understood') as HTMLInputElement;
    understood.checked = true;
    understood.dispatchEvent(new Event('change'));
    fixture.detectChanges();

    expect(cancel.disabled).toBe(false);
    cancel.click();
    expect(cancelRequested).toHaveBeenCalledOnce();
  });

  it('shows no sunk-cost warning before production has started', () => {
    const { fixture, root } = create(3, { cancelReasonId: 6 });

    expect(textOf(fixture)).not.toContain('Production has already started');
    expect(buttonByText(root, 'Cancel PO').disabled).toBe(false);
  });

  it('never lets the button enable without a reason, warning or not', () => {
    const { root } = create(3, { cancelReasonId: null });

    expect(buttonByText(root, 'Cancel PO').disabled).toBe(true);
  });
});
