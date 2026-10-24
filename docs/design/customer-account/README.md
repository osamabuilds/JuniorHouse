# Customer sign-in, sign-up and password reset: design reference

Static design reference for the customer **Sign in**, **Create account** and **forgot-password** pages. These are **designs only**: nothing here is built in the storefront, and there is no spec yet.

| Screen | Desktop (1440) | Mobile (390) |
|---|---|---|
| Sign in | [login.html](login.html) · [png](login.png) | [login-mobile.html](login-mobile.html) · [png](login-mobile.png) |
| Create account | [signup.html](signup.html) · [png](signup.png) | [signup-mobile.html](signup-mobile.html) · [png](signup-mobile.png) |
| Sign in, error state | [login-error.html](login-error.html) · [png](login-error.png) | n/a |
| Forgot password (request) | [forgot-password.html](forgot-password.html) · [png](forgot-password.png) | [forgot-password-mobile.html](forgot-password-mobile.html) · [png](forgot-password-mobile.png) |
| Check your email (sent) | [forgot-password-sent.html](forgot-password-sent.html) · [png](forgot-password-sent.png) | [forgot-password-sent-mobile.html](forgot-password-sent-mobile.html) · [png](forgot-password-sent-mobile.png) |
| Set a new password | [reset-password.html](reset-password.html) · [png](reset-password.png) | [reset-password-mobile.html](reset-password-mobile.html) · [png](reset-password-mobile.png) |

The HTML files are standalone and open in a browser. They load Manrope from Google Fonts for the preview only; the storefront self-hosts its fonts.

Source of truth for the visuals is the design canvas "Romp Storefront Redesign" (boards "Sign in" and "Create account"); these files are an export of it.

## What the design shows

- **Desktop:** a black side panel (brand line, marigold eyebrow) beside the form on white. **Mobile:** one column under a logo-only header.
- **Sign in:** email, password with a Show/Hide button, "Forgot password?", "Keep me signed in", one marigold primary button, Terms and Privacy line.
- **Create account:** first name, last name, email, mobile number, password, a required Terms and Privacy checkbox, an optional newsletter checkbox, one marigold primary button.
- One marigold button per screen, sharp-cornered fields with a 1px black border, labels above fields, tap targets of at least 44px.

## Password reset flow

Three screens: **request** (email field, "Send reset link", back to sign in) → **sent** ("Check your email", resend with a cooldown) → **set a new password** (new and confirm fields, "Update password", a "link expired? request a new one" line).

- The sent message is worded so it does not reveal whether an account exists: "If an account exists for that email address, a reset link is on its way."
- Link lifetime (`[X] minutes`) and resend cooldown (`[X] seconds`) are placeholders to be confirmed.
- Not designed yet: the expired-link page, the "password updated" confirmation and the signed-in state after a reset.

## Before this is built

Customer accounts are not in `docs/specs/` yet: `identity` covers staff only, and its README places customer accounts (registration, password reset, addresses, Child Profiles) in Sprint 10. Per `CLAUDE.md` a spec must be approved before any production code. The design leaves these open; do not resolve them silently:

- **FR-39 vs SEC-02:** hard account lockout, or progressive throttling plus CAPTCHA. This decides the sign-in error and locked-out states, which are not designed.
- **Password rules:** shown as a placeholder (`[Password rules to be confirmed]`).
- **Sign-up fields:** whether a mobile number is collected or used to sign in is not stated by the BRD.
- **Not designed yet:** email verification, the error states of sign-up and reset, and the locked-out state (needs the FR-39 vs SEC-02 decision). The sign-in error state is drawn in [login-error.html](login-error.html).
- **Guest checkout:** `docs/design-system/Pages.md` says checkout is guest by default, so these pages must not gate it.
- **SEO:** account pages are blocked in `robots.txt` and carry `noindex` (SEO-05).
