import { Component } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';

/**
 * The admin shell (SCRUM-174): semantic landmarks (header/nav/main/footer), one per page, with
 * the four Sprint 1 sections in the nav. Every routed screen renders inside <main>.
 */
@Component({
  selector: 'app-root',
  imports: [RouterLink, RouterLinkActive, RouterOutlet],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App {}
