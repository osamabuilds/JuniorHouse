import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-site-footer',
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './site-footer.component.html',
  styleUrl: './site-footer.component.scss',
})
export class SiteFooterComponent {
  protected readonly shop = [
    { label: 'New in', path: '/new-in' },
    { label: 'Girls', path: '/girls' },
    { label: 'Boys', path: '/boys' },
    { label: 'Baby', path: '/baby' },
  ];
  protected readonly help = ['Size guide', 'Delivery', 'Returns', 'Contact'];
  protected readonly company = ['Our story', 'Privacy', 'Terms'];
}
