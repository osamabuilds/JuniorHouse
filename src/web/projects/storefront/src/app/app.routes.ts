import { isDevMode } from '@angular/core';
import { Routes, UrlMatcher, UrlSegment } from '@angular/router';
import { signedInGuard, signedOutGuard } from './core/customer-auth';
import { provideCodConfirm } from './features/orders/cod-confirm/cod-confirm.providers';
import { provideCheckout } from './features/orders/checkout/checkout.providers';
import { provideOrderLookup } from './features/orders/order-lookup/order-lookup.providers';
import { provideAddresses } from './features/customers/addresses/addresses.providers';
import { audienceCanMatch } from './features/storefront/listings/listings.guards';

/** The foundations page is a visual check for the design system; it is not part of production builds. */
const devRoutes: Routes = isDevMode()
  ? [
      {
        path: '_foundations',
        loadComponent: () =>
          import('./foundations/foundations.page').then((m) => m.FoundationsPage),
      },
    ]
  : [];

/**
 * A product address has three parts (/{audience}/{category}/{slug}). Whether it exists is the API answer, which becomes a real 301, 404 or
 * 410 on the product page itself (PDP-13); no other route has three parts.
 */
export const productMatcher: UrlMatcher = (segments: UrlSegment[]) => (segments.length === 3 ? { consumed: segments } : null);

const listing = () => import('./features/storefront/listings/containers/listing-page/listing-page.container').then((m) => m.ListingPageContainer);
const notifyMeResult = () =>
  import('./features/customers/notify-me/containers/notify-me-result-page/notify-me-result-page.container').then((m) => m.NotifyMeResultPageContainer);
const newsletterResult = () =>
  import('./features/customers/newsletter/containers/newsletter-result-page/newsletter-result-page.container').then((m) => m.NewsletterResultPageContainer);

/**
 * Listings run on the read model through the API (listings LST-*). Content pages are served from the settings by slug; the catch-all
 * gives every other address a real 404.
 */
export const routes: Routes = [
  ...devRoutes,
  {
    path: '',
    pathMatch: 'full',
    title: 'Romp Kids | Considered western wear for children',
    loadComponent: () =>
      import('./features/catalog/home/home-page.container').then((m) => m.HomePageContainer),
  },
  { path: 'new-in', data: { kind: 'new-in' }, loadComponent: listing },
  { path: 'sale', data: { kind: 'sale' }, loadComponent: listing },
  { path: 'search', loadComponent: () => import('./features/storefront/search/containers/search-page/search-page.container').then((m) => m.SearchPageContainer) },
  { path: 'collections/:code', data: { kind: 'collection' }, loadComponent: listing },
  {
    matcher: productMatcher,
    loadComponent: () =>
      import('./features/storefront/product-pages/containers/product-page/product-page.container').then((m) => m.ProductPageContainer),
  },
  // Sign-in screens (accounts S9). Signed-in customers are sent on to their account; the screens are noindex (ACT-16).
  { path: 'cod/confirm', data: { shell: 'slim' }, providers: [provideCodConfirm()], loadComponent: () => import('./features/orders/cod-confirm/containers/cod-confirm-page/cod-confirm-page.container').then((m) => m.CodConfirmPageContainer) },
  { path: 'checkout', data: { shell: 'slim' }, providers: [provideCheckout()], loadComponent: () => import('./features/orders/checkout/containers/checkout-page/checkout-page.container').then((m) => m.CheckoutPageContainer) },
  { path: 'order-placed/:number', data: { shell: 'slim' }, providers: [provideCodConfirm()], loadComponent: () => import('./features/orders/confirmation/containers/order-placed-page/order-placed-page.container').then((m) => m.OrderPlacedPageContainer) },
  { path: 'track-order', providers: [provideOrderLookup()], loadComponent: () => import('./features/orders/order-lookup/containers/track-order-page/track-order-page.container').then((m) => m.TrackOrderPageContainer) },
  { path: 'cart', loadComponent: () => import('./features/cart/carts/containers/cart-page/cart-page.container').then((m) => m.CartPageContainer) },
  { path: 'account/sign-in', data: { shell: 'slim' }, canActivate: [signedOutGuard], loadComponent: () => import('./features/customers/sign-in/containers/sign-in-page/sign-in-page.container').then((m) => m.SignInPageContainer) },
  { path: 'account/register', data: { shell: 'slim' }, canActivate: [signedOutGuard], loadComponent: () => import('./features/customers/sign-in/containers/register-page/register-page.container').then((m) => m.RegisterPageContainer) },
  { path: 'account/forgot-password', data: { shell: 'slim' }, loadComponent: () => import('./features/customers/sign-in/containers/forgot-password-page/forgot-password-page.container').then((m) => m.ForgotPasswordPageContainer) },
  { path: 'account/reset-password', data: { shell: 'slim' }, loadComponent: () => import('./features/customers/sign-in/containers/reset-password-page/reset-password-page.container').then((m) => m.ResetPasswordPageContainer) },
  { path: 'account/verify-email', data: { shell: 'slim' }, loadComponent: () => import('./features/customers/sign-in/containers/verify-email-page/verify-email-page.container').then((m) => m.VerifyEmailPageContainer) },
  {
    path: 'account',
    canActivate: [signedInGuard],
    providers: [provideAddresses()],
    loadComponent: () => import('./features/customers/account/containers/account-shell/account-shell.container').then((m) => m.AccountShellContainer),
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'addresses' },
      {
        path: 'addresses',
        loadComponent: () =>
          import('./features/customers/addresses/containers/addresses-page/addresses-page.container').then((m) => m.AddressesPageContainer),
      },
      {
        path: 'preferences',
        loadComponent: () =>
          import('./features/customers/newsletter/containers/preferences-page/preferences-page.container').then((m) => m.PreferencesPageContainer),
      },
    ],
  },
  // Where the storefront server sends a visitor after the footer form without JavaScript and after an unsubscribe link (noindex, never cached).
  { path: 'newsletter/subscribed', data: { kind: 'subscribed' }, loadComponent: newsletterResult },
  { path: 'newsletter/unsubscribed', data: { kind: 'unsubscribed' }, loadComponent: newsletterResult },
  // Where the storefront server sends a visitor after the Notify Me form without JavaScript (noindex, never cached).
  { path: 'notify-me/requested', loadComponent: notifyMeResult },
  {
    path: 'wishlist',
    loadComponent: () => import('./features/customers/wishlist/containers/wishlist-page/wishlist-page.container').then((m) => m.WishlistPageContainer),
  },
  { path: ':gender/new-in', data: { kind: 'new-in' }, loadComponent: listing },
  { path: ':gender/sale', data: { kind: 'sale' }, loadComponent: listing },
  { path: ':gender/:category', data: { kind: 'category' }, loadComponent: listing },
  { path: ':gender', canMatch: [audienceCanMatch], data: { kind: 'audience' }, loadComponent: listing },
  { path: ':slug', loadComponent: () => import('./features/storefront/page/containers/page/page.container').then((m) => m.PageContainer) },
  { path: '**', data: { notFound: true }, loadComponent: () => import('./features/storefront/page/containers/page/page.container').then((m) => m.PageContainer) },
];
