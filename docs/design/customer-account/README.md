# Customer sign-in and sign-up: design reference

Static design reference for the customer **Sign in** and **Create account** pages. These are **designs only**: nothing here is built in the storefront, and there is no spec yet.

| Screen | Desktop (1440) | Mobile (390) |
|---|---|---|
| Sign in | [login.html](login.html) · [png](login.png) | [login-mobile.html](login-mobile.html) · [png](login-mobile.png) |
| Create account | [signup.html](signup.html) · [png](signup.png) | [signup-mobile.html](signup-mobile.html) · [png](signup-mobile.png) |

The HTML files are standalone and open in a browser. They load Manrope from Google Fonts for the preview only; the storefront self-hosts its fonts.

Source of truth for the visuals is the design canvas "Romp Storefront Redesign" (boards "Sign in" and "Create account"); these files are an export of it.

## What the design shows

- **Desktop:** a black side panel (brand line, marigold eyebrow) beside the form on white. **Mobile:** one column under a logo-only header.
- **Sign in:** email, password with a Show/Hide button, "Forgot password?", "Keep me signed in", one marigold primary button, Terms and Privacy line.
- **Create account:** first name, last name, email, mobile number, password, a required Terms and Privacy checkbox, an optional newsletter checkbox, one marigold primary button.
- One marigold button per screen, sharp-cornered fields with a 1px black border, labels above fields, tap targets of at least 44px.

## Before this is built

Customer accounts are not in `docs/specs/` yet: `identity` covers staff only, and its README places customer accounts (registration, password reset, addresses, Child Profiles) in Sprint 10. Per `CLAUDE.md` a spec must be approved before any production code. The design leaves these open; do not resolve them silently:

- **FR-39 vs SEC-02:** hard account lockout, or progressive throttling plus CAPTCHA. This decides the sign-in error and locked-out states, which are not designed.
- **Password rules:** shown as a placeholder (`[Password rules to be confirmed]`).
- **Sign-up fields:** whether a mobile number is collected or used to sign in is not stated by the BRD.
- **Not designed yet:** forgot-password and reset pages, email verification, error and success states.
- **Guest checkout:** `docs/design-system/Pages.md` says checkout is guest by default, so these pages must not gate it.
- **SEO:** account pages are blocked in `robots.txt` and carry `noindex` (SEO-05).
