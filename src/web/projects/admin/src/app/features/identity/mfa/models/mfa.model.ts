/** The secret as it is shown once while enrolling: the address a QR code carries, the same secret as grouped text, and when it lapses. Held in memory only. */
export interface MfaEnrolment {
  readonly otpAuthUri: string;
  readonly secretText: string;
  readonly expiresAt: string;
}

/** Recovery codes, returned once by the API and shown once by the page. */
export interface RecoveryCodesDto {
  readonly recoveryCodes: readonly string[];
}

export interface MfaStatusDto {
  readonly enrolled: boolean;
  readonly recoveryCodesRemaining: number;
}
