import { ChangeDetectionStrategy, Component, OnInit, computed, inject, input, output, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ApiError } from '@core/http';
import { AppDatePipe, ControlErrorsComponent, FieldErrorsComponent } from '@shared';
import { DecideSampleValue, PpSampleRoundDto, SampleFileUpload, SubmitSampleValue } from '../../models';
import { todayInPakistan } from '../../utils';
import { HasPermissionDirective } from '@core/auth';

/**
 * The pre-production sample rounds of a run (spec AC-10..AC-13, D6, D7). Every round is shown with its number,
 * its decision and its photos, so the history of a rejected-then-approved sample stays readable. There is no limit
 * on rounds. A round waiting for a decision can be approved or rejected (a reason is required to reject), and photos
 * or files can be attached to it.
 */
@Component({
  selector: 'app-pp-round-panel',
  imports: [HasPermissionDirective, ReactiveFormsModule, AppDatePipe, ControlErrorsComponent, FieldErrorsComponent],
  template: `
    @if (rounds().length === 0) {
      <p class="state-message state-message--inline">
        No sample has been sent yet. Bulk cutting cannot be recorded until a pre-production sample is approved.
      </p>
    }

    @for (round of rounds(); track round.id) {
      <article class="round" [attr.aria-labelledby]="'round-' + round.roundNo">
        <h3 id="round-{{ round.roundNo }}">
          Round {{ round.roundNo }}
          <span class="badge" [class.badge--muted]="round.decisionCode === 'Pending'" [class.badge--danger]="round.decisionCode === 'Rejected'">
            {{ decisionText(round) }}
          </span>
        </h3>
        <p class="round__meta">
          Sent {{ round.submittedDate | appDate }} &middot; recorded by {{ round.submittedBy }}
          @if (round.note) {
            &middot; {{ round.note }}
          }
        </p>
        @if (round.decidedAt) {
          <p class="round__meta">
            {{ round.decisionCode === 'Approved' ? 'Approved' : 'Rejected' }} {{ round.decidedAt | appDate: 'datetime' }} by
            {{ round.decidedBy }}
            @if (round.decisionReason) {
              &middot; Reason: {{ round.decisionReason }}
            }
          </p>
        }

        @if (round.files.length > 0) {
          <ul class="round__files">
            @for (file of round.files; track file.id) {
              <li>
                <a [href]="fileUrlFor()(round.roundNo, file.id)" download>{{ file.fileName }}</a>
                <span class="round__size">({{ sizeText(file.fileSizeBytes) }})</span>
                @if (round.decisionCode === 'Pending') {
                  <button *hasPermission="'production.write'" type="button" class="button button--small" (click)="fileRemoved.emit({ roundNo: round.roundNo, fileId: file.id })">
                    Remove<span class="sr-only"> {{ file.fileName }}</span>
                  </button>
                }
              </li>
            }
          </ul>
        }

        @if (round.decisionCode === 'Pending' && runOpen()) {
          <div class="round__upload">
            <label [for]="'round-file-' + round.roundNo">Attach a photo or file to round {{ round.roundNo }}</label>
            <input [id]="'round-file-' + round.roundNo" type="file" (change)="onFile(round.roundNo, $event)" [disabled]="fileUploading()" />
            @if (fileUploading()) {
              <p class="field-hint">Uploading…</p>
            }
            @if (fileError()) {
              <p class="banner banner--error" role="alert">{{ fileError()?.message }}</p>
            }
          </div>

          <form class="card decide" [formGroup]="decideForm" (ngSubmit)="noop()" [attr.aria-label]="'Decide round ' + round.roundNo" novalidate>
            <h4>Decide round {{ round.roundNo }}</h4>
            @if (decideError()) {
              <p class="banner banner--error" role="alert">{{ decideError() }}</p>
            }
            <div class="form-grid">
              <div class="form-field">
                <label [for]="'decide-person-' + round.roundNo">Your name</label>
                <input [id]="'decide-person-' + round.roundNo" type="text" formControlName="actingPerson" maxlength="100" autocomplete="name" />
                <app-control-errors [control]="decideForm.controls.actingPerson" label="Your name" />
                <app-field-errors [messages]="fieldErrors('ActingPerson')" />
              </div>
              <div class="form-field">
                <label [for]="'decide-reason-' + round.roundNo">Reason (required to reject)</label>
                <textarea [id]="'decide-reason-' + round.roundNo" rows="2" formControlName="reason" maxlength="500"></textarea>
                <app-field-errors [messages]="fieldErrors('Reason')" />
              </div>
            </div>
            <div class="form-actions">
              <button *hasPermission="'production.write'" type="button" class="button button--primary" [disabled]="saving()" (click)="decide(round.roundNo, true)">Approve sample</button>
              <button *hasPermission="'production.write'" type="button" class="button button--danger" [disabled]="saving()" (click)="decide(round.roundNo, false)">Reject sample</button>
            </div>
          </form>
        }
      </article>
    }

    @if (showSubmitForm()) {
      <form class="card" [formGroup]="submitForm" (ngSubmit)="submit()" aria-labelledby="sample-heading" novalidate>
        <h3 id="sample-heading">Record a sample sent by the vendor</h3>
        @if (error()) {
          <p class="banner banner--error" role="alert">{{ error()?.message }}</p>
        }
        <div class="form-grid">
          <div class="form-field">
            <label for="sample-date">Date the sample arrived</label>
            <input id="sample-date" type="date" formControlName="submittedDate" [min]="minDate()" [max]="today" />
            <app-control-errors [control]="submitForm.controls.submittedDate" label="Date the sample arrived" />
            <app-field-errors [messages]="fieldErrors('SubmittedDate')" />
          </div>
          <div class="form-field">
            <label for="sample-reporter">Who at the vendor sent it</label>
            <input id="sample-reporter" type="text" formControlName="reportedBy" maxlength="100" />
            <app-control-errors [control]="submitForm.controls.reportedBy" label="Vendor contact" />
            <app-field-errors [messages]="fieldErrors('ReportedBy')" />
          </div>
          <div class="form-field">
            <label for="sample-person">Your name</label>
            <input id="sample-person" type="text" formControlName="actingPerson" maxlength="100" autocomplete="name" />
            <app-control-errors [control]="submitForm.controls.actingPerson" label="Your name" />
            <app-field-errors [messages]="fieldErrors('ActingPerson')" />
          </div>
          <div class="form-field form-field--wide">
            <label for="sample-note">Note (optional)</label>
            <textarea id="sample-note" rows="2" formControlName="note" maxlength="500"></textarea>
          </div>
        </div>
        <div class="form-actions">
          <button type="submit" class="button button--primary" [disabled]="saving()">{{ saving() ? 'Saving…' : 'Record sample' }}</button>
          <button type="button" class="button" (click)="cancelled.emit()">Cancel</button>
        </div>
      </form>
    } @else if (canSubmit()) {
      <div class="panel__actions">
        <button *hasPermission="'production.write'" type="button" class="button" (click)="submitOpened.emit()">
          {{ rounds().length === 0 ? 'Record a sample' : 'Record another sample' }}
        </button>
      </div>
    }
  `,
  styles: `
    :host {
      display: block;
    }
    .round {
      border: 1px solid var(--color-border);
      border-radius: var(--radius-md);
      padding: var(--space-4);
      margin-bottom: var(--space-4);
      background: var(--color-surface);
    }
    .round h3 {
      margin: 0 0 var(--space-2);
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: var(--space-2);
    }
    .round__meta {
      margin: 0 0 var(--space-1);
      color: var(--color-ink-soft);
      font-size: var(--text-sm);
    }
    .round__files {
      list-style: none;
      margin: var(--space-3) 0;
      padding: 0;
      display: grid;
      gap: var(--space-2);
    }
    .round__size {
      color: var(--color-ink-soft);
      font-size: var(--text-xs);
    }
    .round__upload {
      margin: var(--space-3) 0;
    }
    .decide {
      margin-top: var(--space-3);
      box-shadow: none;
    }
    .decide h4 {
      margin: 0 0 var(--space-2);
      font-size: var(--text-base);
    }
    .badge--danger {
      background: var(--color-danger-tint);
      color: var(--color-danger);
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PpRoundPanelComponent implements OnInit {
  private readonly formBuilder = inject(FormBuilder);

  readonly rounds = input.required<readonly PpSampleRoundDto[]>();
  /** Whether the API would accept a new round right now (none waiting; no approval unless an amendment superseded it). */
  readonly canSubmit = input.required<boolean>();
  readonly runOpen = input.required<boolean>();
  /** True while the "record a sample" form is open. */
  readonly submitFormOpen = input(false);
  readonly minDate = input.required<string>();
  readonly saving = input(false);
  readonly error = input<ApiError | null>(null);
  readonly fileUploading = input(false);
  readonly fileError = input<ApiError | null>(null);
  readonly defaultPerson = input('');
  readonly fileUrlFor = input.required<(roundNo: number, fileId: number) => string>();

  readonly submitOpened = output<void>();
  readonly cancelled = output<void>();
  readonly submitted = output<SubmitSampleValue>();
  readonly decided = output<DecideSampleValue>();
  readonly fileSelected = output<SampleFileUpload>();
  readonly fileRemoved = output<{ roundNo: number; fileId: number }>();

  readonly today = todayInPakistan();
  readonly decideError = signal<string | null>(null);
  readonly showSubmitForm = computed(() => this.submitFormOpen() && this.canSubmit());

  readonly submitForm = this.formBuilder.group({
    submittedDate: this.formBuilder.nonNullable.control(this.today, Validators.required),
    reportedBy: this.formBuilder.nonNullable.control('', [Validators.required, Validators.maxLength(100)]),
    actingPerson: this.formBuilder.nonNullable.control('', [Validators.required, Validators.minLength(2), Validators.maxLength(100)]),
    note: this.formBuilder.nonNullable.control('', Validators.maxLength(500)),
  });

  readonly decideForm = this.formBuilder.group({
    actingPerson: this.formBuilder.nonNullable.control('', [Validators.required, Validators.minLength(2), Validators.maxLength(100)]),
    reason: this.formBuilder.nonNullable.control('', Validators.maxLength(500)),
  });

  ngOnInit(): void {
    this.submitForm.patchValue({ actingPerson: this.defaultPerson() });
    this.decideForm.patchValue({ actingPerson: this.defaultPerson() });
  }

  fieldErrors(field: string): readonly string[] {
    return this.error()?.fieldErrors[field] ?? [];
  }

  decisionText(round: PpSampleRoundDto): string {
    return round.decisionCode === 'Pending' ? 'Waiting for a decision' : round.decisionCode;
  }

  sizeText(bytes: number): string {
    return bytes < 1024 * 1024 ? `${Math.max(1, Math.round(bytes / 1024))} KB` : `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  }

  noop(): void {
    // The two decision buttons act on their own; Enter in a field must not submit anything.
  }

  onFile(roundNo: number, event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (file) {
      this.fileSelected.emit({ roundNo, file });
      input.value = '';
    }
  }

  decide(roundNo: number, approve: boolean): void {
    const raw = this.decideForm.getRawValue();
    this.decideForm.markAllAsTouched();

    if (this.decideForm.controls.actingPerson.invalid) {
      this.decideError.set("This can't be saved yet. Still needed: your name.");
      return;
    }
    if (!approve && raw.reason.trim().length === 0) {
      this.decideError.set('Say why the sample is rejected, so the vendor can fix it.');
      return;
    }

    this.decideError.set(null);
    this.decided.emit({ roundNo, approve, reason: raw.reason.trim() || null, actingPerson: raw.actingPerson.trim() });
  }

  submit(): void {
    if (this.submitForm.invalid) {
      this.submitForm.markAllAsTouched();
      return;
    }

    const raw = this.submitForm.getRawValue();
    this.submitted.emit({
      submittedDate: raw.submittedDate,
      reportedBy: raw.reportedBy.trim(),
      actingPerson: raw.actingPerson.trim(),
      note: raw.note.trim() || null,
    });
  }
}
