import { DatePipe, DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, effect, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Store } from '@ngrx/store';
import { HasPermissionDirective } from '@core/auth';
import { ReferenceLookupActions, selectLookup } from '@features/reference/lookups';
import { PaginationComponent } from '@shared';
import { DiscountCodeDto, DiscountCodeInput, DiscountKind } from '../../models';
import {
  DiscountCodesPageActions,
  selectCodes,
  selectEditing,
  selectError,
  selectLoading,
  selectNotice,
  selectPage,
  selectPageSize,
  selectSaving,
  selectSwitchingId,
  selectTotal,
} from '../../store';

/** An ISO time as the value a datetime-local box wants (the person's own time zone), or empty. */
export function toLocalInput(iso: string | null): string {
  if (!iso) return '';
  const d = new Date(iso);
  const pad = (n: number): string => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

/**
 * Discount codes (docs/specs/cart/discount-codes, FR-57): create, change, switch off and list. A code is never deleted, so past orders keep
 * their history; switching it off makes shoppers see "not valid". The server checks every value again and answers in words (DSC-12).
 */
@Component({
  selector: 'app-discount-codes-page',
  imports: [ReactiveFormsModule, HasPermissionDirective, PaginationComponent, DecimalPipe, DatePipe],
  templateUrl: './discount-codes-page.container.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DiscountCodesPageContainer {
  private readonly store = inject(Store);
  private readonly fb = inject(FormBuilder);

  readonly codes = this.store.selectSignal(selectCodes);
  readonly total = this.store.selectSignal(selectTotal);
  readonly currentPage = this.store.selectSignal(selectPage);
  readonly pageSize = this.store.selectSignal(selectPageSize);
  readonly loading = this.store.selectSignal(selectLoading);
  readonly saving = this.store.selectSignal(selectSaving);
  readonly switchingId = this.store.selectSignal(selectSwitchingId);
  readonly editing = this.store.selectSignal(selectEditing);
  readonly error = this.store.selectSignal(selectError);
  readonly notice = this.store.selectSignal(selectNotice);
  readonly categories = this.store.selectSignal(selectLookup('categories'));

  readonly form = this.fb.group({
    code: this.fb.nonNullable.control('', [Validators.required, Validators.minLength(3), Validators.maxLength(32), Validators.pattern(/^[A-Za-z0-9-]+$/)]),
    kind: this.fb.nonNullable.control<DiscountKind>('percentage'),
    value: this.fb.control<number | null>(null, [Validators.required, Validators.min(0.01)]),
    minimumSpend: this.fb.control<number | null>(null, [Validators.min(0)]),
    expiresAt: this.fb.nonNullable.control(''),
    usageCap: this.fb.control<number | null>(null, [Validators.min(1)]),
    perCustomerCap: this.fb.control<number | null>(1, [Validators.min(1)]),
    includesSaleItems: this.fb.nonNullable.control(false),
    categoryIds: this.fb.nonNullable.control<readonly number[]>([]),
  });

  constructor() {
    this.store.dispatch(ReferenceLookupActions.requested({ typeKey: 'categories', includeInactive: false }));
    this.store.dispatch(DiscountCodesPageActions.opened());

    // The form shows the code being changed, and goes back to empty when the change ends.
    effect(() => {
      const code = this.editing();
      if (code) {
        this.form.reset({
          code: code.code,
          kind: code.kind,
          value: code.value,
          minimumSpend: code.minimumSpend,
          expiresAt: toLocalInput(code.expiresAt),
          usageCap: code.usageCap,
          perCustomerCap: code.perCustomerCap,
          includesSaleItems: code.includesSaleItems,
          categoryIds: code.categoryIds,
        });
      } else {
        this.form.reset({ code: '', kind: 'percentage', value: null, minimumSpend: null, expiresAt: '', usageCap: null, perCustomerCap: 1, includesSaleItems: false, categoryIds: [] });
      }
    });
  }

  submit(): void {
    this.form.markAllAsTouched();
    if (this.form.invalid) return;
    const v = this.form.getRawValue();
    const input: DiscountCodeInput = {
      code: v.code.trim(),
      kind: v.kind,
      value: v.value ?? 0,
      minimumSpend: v.minimumSpend ?? null,
      expiresAt: v.expiresAt ? new Date(v.expiresAt).toISOString() : null,
      usageCap: v.usageCap ?? null,
      perCustomerCap: v.perCustomerCap ?? null,
      includesSaleItems: v.includesSaleItems,
      categoryIds: v.categoryIds,
    };
    this.store.dispatch(DiscountCodesPageActions.submitted({ input }));
  }

  toggleCategory(id: number, checked: boolean): void {
    const current = this.form.controls.categoryIds.value;
    this.form.controls.categoryIds.setValue(checked ? [...current.filter((c) => c !== id), id] : current.filter((c) => c !== id));
  }

  hasCategory(id: number): boolean {
    return this.form.controls.categoryIds.value.includes(id);
  }

  edit(code: DiscountCodeDto): void {
    this.store.dispatch(DiscountCodesPageActions.editStarted({ id: code.id }));
  }

  cancelEdit(): void {
    this.store.dispatch(DiscountCodesPageActions.editCancelled());
  }

  toggle(code: DiscountCodeDto): void {
    this.store.dispatch(DiscountCodesPageActions.switched({ id: code.id, active: !code.isActive }));
  }

  dismissNotice(): void {
    this.store.dispatch(DiscountCodesPageActions.noticeDismissed());
  }

  changePage(page: number): void {
    this.store.dispatch(DiscountCodesPageActions.pageChanged({ page }));
  }

  changePageSize(pageSize: number): void {
    this.store.dispatch(DiscountCodesPageActions.pageSizeChanged({ pageSize }));
  }

  /** "10%" or "Rs. 500" for the list. */
  valueText(code: DiscountCodeDto): string {
    return code.kind === 'percentage' ? `${code.value}%` : `Rs. ${code.value}`;
  }
}
