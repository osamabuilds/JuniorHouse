import { RenderMode, ServerRoute } from '@angular/ssr';

export const serverRoutes: ServerRoute[] = [
  {
    path: '',
    renderMode: RenderMode.Prerender,
  },
  {
    // Parameterised catalogue routes are rendered per request (SEO-01) until prerender params come from the API.
    path: '**',
    renderMode: RenderMode.Server,
  },
];
