import { ChangeDetectionStrategy, Component, signal } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';

@Component({
  selector: 'app-site-header',
  imports: [RouterLink, RouterLinkActive],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './site-header.component.html',
  styleUrl: './site-header.component.scss',
})
export class SiteHeaderComponent {
  protected readonly menuOpen = signal(false);

  protected readonly links = [
    { label: 'New in', path: '/new-in' },
    { label: 'Girls', path: '/girls' },
    { label: 'Boys', path: '/boys' },
    { label: 'Baby', path: '/baby' },
    { label: 'Sale', path: '/sale' },
  ];

  protected toggleMenu(): void {
    this.menuOpen.update((open) => !open);
  }

  protected closeMenu(): void {
    this.menuOpen.set(false);
  }
}
