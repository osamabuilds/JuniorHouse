import { TestBed } from '@angular/core/testing';
import { provideAllowAllPermissions } from '@app/testing/provide-allow-all-permissions';
import { DeliveryDetail, DeliveryLogRow } from '../../models';
import { DeliveryDetailComponent, ResendRequest } from './delivery-detail.component';

const row: DeliveryLogRow = {
  id: 11, notificationId: 4, kind: 'ORCF', channel: 'EMAL', status: 'FAIL', attempts: 2, lastError: 'PERM', orderId: 7, orderNumber: 'RMP-2026-00007-1',
  recipient: 'a***@mail.com', createdAt: '2026-11-04T09:00:00Z', nextAttemptAt: null, isResend: false, resentBy: null, canResend: true,
};

function detailOf(patch: Partial<DeliveryDetail> = {}, rowPatch: Partial<DeliveryLogRow> = {}): DeliveryDetail {
  return {
    row: { ...row, ...rowPatch },
    expiresAt: '2026-11-11T09:00:00Z',
    resendOfId: null,
    attempts: [{ attemptNumber: 1, outcome: 'FAIL', error: 'PERM', at: '2026-11-04T09:00:05Z' }],
    deadAt: null,
    resentBy: null,
    resentAt: null,
    warnsOfDuplicate: false,
    ...patch,
  };
}

describe('DeliveryDetailComponent (spec DLG-1, DLG-2, DLG-6, DLG-7, DLG-8)', () => {
  beforeEach(() => TestBed.configureTestingModule({ providers: [provideAllowAllPermissions()] }));

  function create(detail = detailOf(), resending = false) {
    const fixture = TestBed.createComponent(DeliveryDetailComponent);
    fixture.componentRef.setInput('detail', detail);
    fixture.componentRef.setInput('resending', resending);
    const requests: ResendRequest[] = [];
    fixture.componentInstance.resendRequested.subscribe((r) => requests.push(r));
    fixture.detectChanges();
    const root = fixture.nativeElement as HTMLElement;
    const button = (text: string) => Array.from(root.querySelectorAll('button')).find((b) => b.textContent?.includes(text)) as HTMLButtonElement | undefined;
    const click = (text: string) => {
      button(text)?.click();
      fixture.detectChanges();
    };
    return { fixture, root, requests, button, click };
  }

  it('DLG-1 shows the masked recipient, the status in plain words and each attempt with its problem', () => {
    const { root } = create();

    expect(root.textContent).toContain('a***@mail.com');
    expect(root.textContent).toContain('Failed');
    expect(root.textContent).toContain('Refused by the provider');
    expect(root.querySelectorAll('tbody tr')).toHaveLength(1);
  });

  it('DLG-2 a failed message is resent after one confirmation, with a key', () => {
    const { requests, click } = create();

    click('Resend');
    click('Yes, resend it');

    expect(requests).toHaveLength(1);
    expect(requests[0].id).toBe(11);
    expect(requests[0].key).toMatch(/^rs-[0-9a-f]{32}$/);
    expect(requests[0].acknowledgeDuplicate).toBe(false);
  });

  it('DLG-7 a message that was probably received warns of a duplicate and sends the yes with the request', () => {
    const { root, requests, click } = create(detailOf({ warnsOfDuplicate: true }, { status: 'SENT', lastError: null }));

    click('Resend');

    expect((root.querySelector('[role="alert"]') as HTMLElement).textContent).toContain('second copy');
    click('Yes, resend it');
    expect(requests[0].acknowledgeDuplicate).toBe(true);
  });

  it('DLG-8 the resend button waits while a resend is on its way', () => {
    const { button } = create(detailOf(), true);

    expect(button('Resend')?.disabled).toBe(true);
  });

  it('DLG-2 leaving it asks nothing of the server', () => {
    const { requests, click, button } = create();

    click('Resend');
    click('No, leave it');

    expect(requests).toHaveLength(0);
    expect(button('Resend')).toBeTruthy();
  });

  it('DLG-2 a queued or skipped message offers no resend', () => {
    const { root, button } = create(detailOf({}, { status: 'QUED', canResend: false }));

    expect(button('Resend')).toBeUndefined();
    expect(root.textContent).toContain('cannot be resent');
  });

  it('DLG-3 a dead letter that was resent says who did it and when', () => {
    const { root } = create(detailOf({ deadAt: '2026-11-04T10:00:00Z', resentBy: 'support@romp.pk', resentAt: '2026-11-04T11:00:00Z' }, { status: 'DEAD' }));

    expect(root.textContent).toContain('resent by support@romp.pk');
  });
});
