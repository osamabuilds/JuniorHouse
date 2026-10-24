import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { PageRow } from '../../models';

/** The content pages, one row each. The title is a real link to the editor; the status is words, not only a colour. */
@Component({
  selector: 'app-pages-table',
  imports: [RouterLink],
  template: `
    @if (loading()) {
      <p class="state-message" role="status">Loading the pages…</p>
    } @else if (pages().length === 0) {
      <p class="state-message">There are no pages here.</p>
    } @else {
      <div class="table-scroll">
        <table>
          <caption class="sr-only">Content pages</caption>
          <thead>
            <tr>
              <th scope="col">Page</th>
              <th scope="col">Address</th>
              <th scope="col">Status</th>
              <th scope="col">Blocks</th>
            </tr>
          </thead>
          <tbody>
            @for (page of pages(); track page.id) {
              <tr>
                <th scope="row"><a [routerLink]="['/storefront/pages', page.id]">{{ page.title }}</a></th>
                <td>/{{ page.slug }}</td>
                <td><span class="badge" [class.badge--muted]="page.status !== 'Published'">{{ page.status }}</span></td>
                <td>{{ page.blockCount }}</td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    }
  `,
  styles: '.table-scroll { overflow-x: auto; }',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PagesTableComponent {
  readonly pages = input.required<readonly PageRow[]>();
  readonly loading = input(false);
}
