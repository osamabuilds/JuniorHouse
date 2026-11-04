/** One line of the live checklist. The codes and wording are the server's (PasswordRule), so a refusal can be mapped back by code. */
export interface PasswordCheck {
  readonly code: string;
  readonly message: string;
  readonly met: boolean;
}

/** Mirrors the API rule (8 to 128 characters, a capital, a small letter, a digit, no spaces). The API enforces it again on every request. */
export function checkPassword(password: string): readonly PasswordCheck[] {
  return [
    { code: 'LENGTH', message: '8 to 128 characters', met: password.length >= 8 && password.length <= 128 },
    { code: 'CAPITAL', message: 'A capital letter', met: /\p{Lu}/u.test(password) },
    { code: 'SMALL', message: 'A small letter', met: /\p{Ll}/u.test(password) },
    { code: 'DIGIT', message: 'A number', met: /\p{Nd}/u.test(password) },
    { code: 'NO_SPACES', message: 'No spaces', met: !/\s/u.test(password) },
  ];
}

export const isPasswordValid = (password: string): boolean => checkPassword(password).every((c) => c.met);
