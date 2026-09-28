import { Component, computed, inject, input, output, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ApiError } from '../core/api-error';
import { LookupDto } from '../reference-data/reference-api.service';
import { FieldErrors } from '../shared/field-errors';

import { AmendForm, StyleCell } from './amend-form';
import { AmendmentValue, PoDto, PoFileDto, VendorResponseValue } from './po-api.service';

export const OUTCOME_CONFIRMED = 1;
export const OUTCOME_COUNTERED = 2;
export const OUTCOME_DECLINED = 3;

/**
 * SCRUM-93 task 49 (AC-25..AC-31): records what the vendor said, replacing the bare Acknowledge
 * button. Confirmed acknowledges the PO; Countered opens the amendment form pre-flagged as
 * vendor-initiated (it becomes a Pending revision for Romp to decide); Declined records the
 * refusal and leaves the PO untouched (staff then cancel it with "Vendor declined" pre-selected).
 * Channel and who responded are always required; the response time defaults to now.
 */
@Component({
  selector: 'app-vendor-response-form',
  imports: [ReactiveFormsModule, FieldErrors, AmendForm],
  template: `
    <section class="vresp card" aria-labelledby="vresp-heading">
      <h3 id="vresp-heading">Record vendor response</h3>

      @if (error() && outcome() !== 2) {
        <p class="banner banner--error" role="alert">{{ error()?.message }}</p>
      }

      <form [formGroup]="form" (ngSubmit)="submitSimple()">
        <div class="form-grid">
          <div class="form-field">
            <label for="vresp-outcome">What did the vendor say?</label>
            <select id="vresp-outcome" formControlName="outcomeTypeId">
              <option [ngValue]="1">Confirmed as sent</option>
              <option [ngValue]="2">Countered with different terms</option>
              <option [ngValue]="3">Declined</option>
            </select>
          </div>

          <div class="form-field">
            <label for="vresp-channel">How did they respond?</label>
            <select id="vresp-channel" formControlName="channelId">
              <option [ngValue]="null">Select a channel…</option>
              @for (channel of channels(); track channel.id) {
                <option [ngValue]="channel.id">{{ channel.name }}</option>
              }
            </select>
            @if (attempted() && form.controls.channelId.invalid) {
              <p class="field-error" role="alert">Choose the channel the vendor used.</p>
            }
            <app-field-errors [messages]="fieldErrors('ChannelId')" />
          </div>

          <div class="form-field">
            <label for="vresp-responder">Who at the vendor responded?</label>
            <input id="vresp-responder" type="text" formControlName="responderName" />
            @if (attempted() && form.controls.responderName.invalid) {
              <p class="field-error" role="alert">Enter the name of the person who responded.</p>
            }
            <app-field-errors [messages]="fieldErrors('ResponderName')" />
          </div>

          <div class="form-field">
            <label for="vresp-time">When (leave empty for now)</label>
            <input id="vresp-time" type="datetime-local" formControlName="responseDte" />
            <app-field-errors [messages]="fieldErrors('ResponseDte')" />
          </div>
        </div>

        <p class="field-hint">Responding to revision {{ currentRevisionNumber() }}, the PO's current in-force revision.</p>

        @if (outcome() !== 2) {
          <div class="form-actions">
            <button type="submit" class="button button--primary" [disabled]="saving()">
              {{ saving() ? 'Saving…' : 'Record response' }}
            </button>
            <button type="button" class="button" (click)="cancelled.emit()">Cancel</button>
          </div>
        }
      </form>

      @if (outcome() === 2) {
        <app-amend-form
          mode="counter"
          [po]="po()"
          [initiatorId]="2"
          [cells]="cells()"
          [reasons]="reasons()"
          [fabricOptions]="fabricOptions()"
          [files]="files()"
          [error]="error()"
          [saving]="saving()"
          (amended)="submitCounter($event)"
          (cancelled)="cancelled.emit()"
        />
      }
    </section>
  `,
  styles: `
    .vresp {
      margin-bottom: var(--space-5);
    }
    .field-error {
      color: var(--color-danger);
      font-size: 0.8125rem;
      margin: var(--space-1) 0 0;
    }
  `,
})
export class VendorResponseForm {
  private readonly formBuilder = inject(FormBuilder);

  readonly po = input.required<PoDto>();
  readonly currentRevisionNumber = input(0);
  readonly channels = input<readonly LookupDto[]>([]);
  readonly reasons = input<readonly LookupDto[]>([]);
  readonly fabricOptions = input<readonly LookupDto[]>([]);
  readonly cells = input<readonly StyleCell[]>([]);
  readonly files = input<readonly PoFileDto[]>([]);
  readonly error = input<ApiError | null>(null);
  readonly saving = input(false);

  readonly recorded = output<VendorResponseValue>();
  readonly cancelled = output<void>();

  readonly attempted = signal(false);

  readonly form = this.formBuilder.group({
    outcomeTypeId: this.formBuilder.nonNullable.control(OUTCOME_CONFIRMED),
    channelId: this.formBuilder.control<number | null>(null, Validators.required),
    responderName: this.formBuilder.nonNullable.control('', Validators.required),
    responseDte: this.formBuilder.nonNullable.control(''),
  });

  private readonly outcomeSignal = signal(OUTCOME_CONFIRMED);
  readonly outcome = computed(() => this.outcomeSignal());

  constructor() {
    this.form.controls.outcomeTypeId.valueChanges.subscribe((value) => this.outcomeSignal.set(value));
  }

  fieldErrors(field: string): readonly string[] {
    return this.error()?.fieldErrors[field] ?? [];
  }



  submitSimple(): void {
    const value = this.baseValue(null);
    if (value) {
      this.recorded.emit(value);
    }
  }

  submitCounter(amendment: AmendmentValue): void {
    const value = this.baseValue({
      reasonId: amendment.reasonId,
      impactNote: amendment.impactNote,
      vendorMessage: amendment.vendorMessage,
      unitCost: amendment.unitCost,
      expectedDeliveryDate: amendment.expectedDeliveryDate,
      latestAcceptableDate: amendment.latestAcceptableDate,
      overTolerancePercent: amendment.overTolerancePercent,
      underTolerancePercent: amendment.underTolerancePercent,
      paymentTermId: amendment.paymentTermId,
      advancePercent: amendment.advancePercent,
      fabricResponsibilityId: amendment.fabricResponsibilityId,
      lines: amendment.lines,
    });
    if (value) {
      this.recorded.emit(value);
    }
  }

  private baseValue(counter: VendorResponseValue['counter']): VendorResponseValue | null {
    this.attempted.set(true);
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return null;
    }

    const raw = this.form.getRawValue();
    return {
      outcomeTypeId: this.outcomeSignal(),
      revisionNumber: this.currentRevisionNumber(),
      channelId: raw.channelId!,
      responderName: raw.responderName.trim(),
      responseDte: raw.responseDte ? new Date(raw.responseDte).toISOString() : null,
      counter,
    };
  }
}
