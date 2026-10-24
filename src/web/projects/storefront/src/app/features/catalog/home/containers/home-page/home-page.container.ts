import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ProductCardComponent } from '../../../../../shared/components/product-card/product-card.component';
import { SAMPLE_PRODUCTS } from '../../../catalog.sample-data';

@Component({
  selector: 'app-home-page',
  imports: [RouterLink, ProductCardComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './home-page.container.html',
  styleUrl: './home-page.container.scss',
})
export class HomePageContainer {
  protected readonly collections = [
    { label: 'Girls', path: '/girls' },
    { label: 'Boys', path: '/boys' },
    { label: 'Essentials', path: '/new-in' },
  ];
  protected readonly edit = SAMPLE_PRODUCTS.slice(0, 4);
  protected readonly promises = [
    {
      title: 'Size guide on every piece',
      text: 'Measurements by age, so you choose with confidence.',
    },
    { title: 'Nationwide delivery', text: 'Tracked from our warehouse to your door.' },
    { title: 'Easy returns', text: 'Wrong size? We will make it right.' },
    { title: 'Child profiles', text: 'Save sizes for each child, kept private to you.' },
  ];
}
