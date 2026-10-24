import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: '',
    title: 'Romp Kids | Considered western wear for children',
    loadComponent: () =>
      import('./features/catalog/home/containers/home-page/home-page.container').then(
        (m) => m.HomePageContainer,
      ),
  },
  {
    path: ':category',
    title: 'Shop | Romp Kids',
    loadComponent: () =>
      import('./features/catalog/category/containers/category-page/category-page.container').then(
        (m) => m.CategoryPageContainer,
      ),
  },
  {
    path: ':category/:type/:slug',
    title: 'Product | Romp Kids',
    loadComponent: () =>
      import('./features/catalog/product-detail/containers/product-page/product-page.container').then(
        (m) => m.ProductPageContainer,
      ),
  },
];
