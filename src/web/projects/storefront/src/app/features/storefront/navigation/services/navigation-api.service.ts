import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { Home } from '../models/home.model';
import { Menu } from '../models/menu.model';

/** HTTP only: the menu the server builds from lookups and the published catalogue. */
@Injectable({ providedIn: 'root' })
export class NavigationApi {
  private readonly http = inject(HttpClient);

  getHome(): Observable<Home> {
    return this.http.get<Home>('/api/storefront/home');
  }

  getMenu(): Observable<Menu> {
    return this.http.get<Menu>('/api/storefront/menu');
  }
}
