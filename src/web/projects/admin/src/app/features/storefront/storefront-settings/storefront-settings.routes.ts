import { Routes } from '@angular/router';
import { provideEffects } from '@ngrx/effects';
import { provideState } from '@ngrx/store';
import * as storefrontSettingsEffects from './store/storefront-settings.effects';
import { storefrontSettingsReducer } from './store/storefront-settings.reducer';
import { STOREFRONT_SETTINGS_FEATURE_KEY } from './store/storefront-settings.state';
import { unsavedChangesGuard } from './utils/unsaved-changes.guard';

/** One store for all the settings screens; each screen opens it afresh. */
export const STOREFRONT_SETTINGS_ROUTES: Routes = [
  {
    path: '',
    providers: [provideState(STOREFRONT_SETTINGS_FEATURE_KEY, storefrontSettingsReducer), provideEffects(storefrontSettingsEffects)],
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'navigation' },
      {
        path: 'navigation',
        loadComponent: () => import('./containers/navigation-page/navigation-page.container').then((m) => m.NavigationPageContainer),
        title: 'Navigation · Storefront · Romp Admin',
        canDeactivate: [unsavedChangesGuard],
      },
      {
        path: 'ages',
        loadComponent: () => import('./containers/ages-page/ages-page.container').then((m) => m.AgesPageContainer),
        title: 'Ages · Storefront · Romp Admin',
        canDeactivate: [unsavedChangesGuard],
      },
      {
        path: 'footer',
        loadComponent: () => import('./containers/footer-page/footer-page.container').then((m) => m.FooterPageContainer),
        title: 'Footer · Storefront · Romp Admin',
        canDeactivate: [unsavedChangesGuard],
      },
      {
        path: 'contact',
        loadComponent: () => import('./containers/contact-page/contact-page.container').then((m) => m.ContactPageContainer),
        title: 'Settings · Storefront · Romp Admin',
        canDeactivate: [unsavedChangesGuard],
      },
      {
        path: 'delivery',
        loadComponent: () => import('./containers/delivery-page/delivery-page.container').then((m) => m.DeliveryPageContainer),
        title: 'Delivery and exchange · Storefront · Romp Admin',
        canDeactivate: [unsavedChangesGuard],
      },
      {
        path: 'pages',
        loadComponent: () => import('./containers/pages-list-page/pages-list-page.container').then((m) => m.PagesListPageContainer),
        title: 'Pages · Storefront · Romp Admin',
      },
      {
        path: 'pages/new',
        loadComponent: () => import('./containers/page-editor-page/page-editor-page.container').then((m) => m.PageEditorPageContainer),
        title: 'New page · Storefront · Romp Admin',
        canDeactivate: [unsavedChangesGuard],
      },
      {
        path: 'pages/:id',
        loadComponent: () => import('./containers/page-editor-page/page-editor-page.container').then((m) => m.PageEditorPageContainer),
        title: 'Edit page · Storefront · Romp Admin',
        canDeactivate: [unsavedChangesGuard],
      },
      {
        path: 'publish',
        loadComponent: () => import('./containers/publish-page/publish-page.container').then((m) => m.PublishPageContainer),
        title: 'Publish · Storefront · Romp Admin',
      },
    ],
  },
];
