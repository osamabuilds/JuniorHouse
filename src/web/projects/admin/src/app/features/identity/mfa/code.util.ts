/** A code typed from an authenticator app: six digits, which apps often show with a space in the middle. */
export const digitsOnly = (typed: string): string => typed.replace(/\s+/g, '');

export const isSixDigits = (typed: string): boolean => /^\d{6}$/.test(digitsOnly(typed));
