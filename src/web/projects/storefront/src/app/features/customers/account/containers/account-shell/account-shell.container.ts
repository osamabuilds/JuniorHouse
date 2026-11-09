import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';

/** The frame around every signed-in customer page: one navigation, then the page. The page owns its h1; the app shell owns main. */
@Component({
  selector: 'rp-account-shell',
  imports: [RouterLink, RouterLinkActive, RouterOutlet],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './account-shell.container.html',
  styleUrl: './account-shell.container.scss',
})
export class AccountShellContainer {}
